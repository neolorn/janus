using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Sending;
using Janus.Core;
using Janus.Privacy.SubjectKeys;

namespace Janus.Storage.Authentication.Sending;

/// <summary>
/// The messages undertaken but not yet carried, over the <c>send_outbox</c> table.
/// </summary>
/// <param name="context">The context the operation's writes are tracked on.</param>
/// <param name="keyEncryptionKeys">The versions a data key may be wrapped under.</param>
/// <param name="randomness">The randomness the key and the vectors are drawn from.</param>
/// <remarks>
/// Implements D-022, IDN-PRIN-003 and PRIV-RIGHT-005a. The whole message is one
/// encrypted document under a key the row carries, so removing the row removes both
/// the message and the only key that reads it.
/// </remarks>
internal sealed class SendDeliveryStore(
    JanusDbContext context,
    KeyEncryptionKeys keyEncryptionKeys,
    RandomNumberGenerator randomness) : ISendOutbox
{
    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException">The delivery is absent.</exception>
    public async ValueTask AddAsync(SendDelivery delivery, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(delivery);

        byte[] dataKey = PersonalFieldCipher.NewDataKey(randomness);

        try
        {
            var record = new SendDeliveryRecord
            {
                Id = delivery.Id,
                RecordedAt = delivery.RecordedAt,
                Subject = delivery.Requested.Subject,
                KeyVersion = keyEncryptionKeys.CurrentVersion,
                WrappedKey = PersonalFieldCipher.Wrap(dataKey, keyEncryptionKeys.Current.Span),
                Message = Written(dataKey, delivery.Requested),
            };

            await context.SendOutbox.AddAsync(record, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(dataKey);
        }
    }

    /// <inheritdoc/>
    public async ValueTask<SendDelivery?> FindAsync(
        SendDeliveryId delivery,
        CancellationToken cancellationToken)
    {
        SendDeliveryRecord? record = await context.SendOutbox
            .FindAsync([delivery], cancellationToken)
            .ConfigureAwait(false);

        return record is null ? null : Read(record);
    }

    /// <inheritdoc/>
    public async ValueTask RemoveAsync(SendDeliveryId delivery, CancellationToken cancellationToken)
    {
        SendDeliveryRecord? record = await context.SendOutbox
            .FindAsync([delivery], cancellationToken)
            .ConfigureAwait(false);

        if (record is not null)
        {
            context.SendOutbox.Remove(record);
        }
    }

    // A message concerning no account is bound to no subject; the data key is the
    // row's own either way, so nothing else reads what this row holds.
    private static PersonalFieldLocation Located(SubjectId? subject) =>
        new(
            subject ?? default,
            SendDeliveryConfiguration.Table,
            SendDeliveryConfiguration.MessageColumn);

    // A destination this library wrote is a destination this library accepts, so a
    // stored form that no longer parses is a corrupted row and not a message to drop
    // quietly.
    private static SendDestination Destination(SendKind kind, string canonical)
    {
        if (kind is SendKind.Email)
        {
            return EmailAddress.TryParse(canonical, out EmailAddress address)
                ? SendDestination.Of(address)
                : throw new InvalidOperationException("The stored address is not an address.");
        }

        return PhoneNumber.TryParse(canonical, out PhoneNumber number)
            ? SendDestination.Of(number)
            : throw new InvalidOperationException("The stored number is not a number.");
    }

    private byte[] Written(ReadOnlySpan<byte> dataKey, SendRequest request)
    {
        var document = new SendDeliveryDocument(
            VocabularyConverter<SendKind>.Write(request.Kind),
            request.Destination.Canonical,
            VocabularyConverter<MessageKind>.Write(request.Message),
            VocabularyConverter<RestrictionPurpose>.Write(request.Purpose),
            request.Source,
            request.Language,
            request.Subject?.Value,
            request.Values);

        return PersonalFieldCipher.Encrypt(
            dataKey,
            Located(request.Subject),
            JsonSerializer.SerializeToUtf8Bytes(document, SendDeliveryJson.Default.SendDeliveryDocument),
            randomness);
    }

    private SendDelivery Read(SendDeliveryRecord record)
    {
        byte[] dataKey = PersonalFieldCipher.Unwrap(
            PersonalDataFormat.Marker,
            record.KeyVersion,
            record.WrappedKey,
            keyEncryptionKeys);

        SendDeliveryDocument document;

        try
        {
            document = JsonSerializer.Deserialize(
                PersonalFieldCipher.Decrypt(dataKey, Located(record.Subject), record.Message),
                SendDeliveryJson.Default.SendDeliveryDocument)
                ?? throw new InvalidOperationException("The undelivered message is not a document.");
        }
        finally
        {
            CryptographicOperations.ZeroMemory(dataKey);
        }

        SendKind kind = VocabularyConverter<SendKind>.Read(document.Kind);

        var request = new SendRequest(
            Destination(kind, document.Destination),
            VocabularyConverter<MessageKind>.Read(document.Message),
            VocabularyConverter<RestrictionPurpose>.Read(document.Purpose),
            document.Source,
            document.Language)
        {
            Subject = document.Subject is Guid subject ? new SubjectId(subject) : null,
            Values = new Dictionary<string, string>(document.Values, StringComparer.Ordinal),
        };

        return new SendDelivery(record.Id, record.RecordedAt, request);
    }
}
