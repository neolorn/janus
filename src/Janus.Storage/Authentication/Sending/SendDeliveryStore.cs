using System;
using System.Buffers.Text;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Sending;
using Janus.Core;
using Janus.Storage.Privacy.SubjectKeys;
using Microsoft.EntityFrameworkCore;

namespace Janus.Storage.Authentication.Sending;

/// <summary>
/// The messages admitted but not yet carried, over the <c>send_outbox</c> table.
/// </summary>
/// <param name="context">The context the operation's writes are tracked on.</param>
/// <param name="deployment">The deployment's data key, which the row's own key is wrapped under.</param>
/// <param name="randomness">The randomness the key and the vectors are drawn from.</param>
/// <remarks>
/// Implements D-022, AUTH-ABUSE-004, INF-BG-001, IDN-PRIN-003, PRIV-RIGHT-005a and
/// CONV-DESIGN-003. The whole message is one encrypted document under a key the row
/// carries, so removing the row removes both the message and the only key that reads
/// it. A row is carried under a claim: one conditional update marks it claimed until an
/// instant, and what the attempt made of it is written by one statement conditional on
/// that instant, so two passes over the same rows carry each once.
/// </remarks>
internal sealed class SendDeliveryStore(
    StoreContext context,
    DeploymentDataKeyStore deployment,
    RandomNumberGenerator randomness) : ISendOutbox
{
    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException">The delivery is absent.</exception>
    public async ValueTask AddAsync(SendDelivery delivery, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(delivery);

        byte[] dataKey = PersonalFieldCipher.NewDataKey(randomness);
        byte[] deploymentKey = await deployment.UnwrappedAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            var record = new SendDeliveryRecord
            {
                Id = delivery.Id,
                RecordedAt = delivery.RecordedAt,
                Attempts = delivery.Attempts,
                NextAttemptAt = delivery.NextAttemptAt,
                Subject = delivery.Requested.Subject,
                WrappedKey = PersonalFieldCipher.Wrap(dataKey, deploymentKey),
                Reference = SendReferences.Of(delivery.Reference),
                Message = Written(dataKey, delivery),
            };

            await context.SendOutbox.AddAsync(record, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(dataKey);
            CryptographicOperations.ZeroMemory(deploymentKey);
        }
    }

    /// <inheritdoc/>
    public async ValueTask<IReadOnlyList<SendDeliveryId>> DueAsync(
        DateTimeOffset now,
        int count,
        CancellationToken cancellationToken) =>
        await context.SendOutbox
            .AsNoTracking()
            .Where(delivery => delivery.NextAttemptAt <= now
                && (delivery.ClaimedUntil == null || delivery.ClaimedUntil <= now))
            .OrderBy(delivery => delivery.Id)
            .Select(delivery => delivery.Id)
            .Take(count)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc/>
    public async ValueTask<SendClaim?> ClaimAsync(
        SendDeliveryId delivery,
        DateTimeOffset now,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        DateTimeOffset until = RowClaim.Until(now, timeout);

        int claimed = await context.SendOutbox
            .Where(row => row.Id == delivery
                && row.NextAttemptAt <= now
                && (row.ClaimedUntil == null || row.ClaimedUntil <= now))
            .ExecuteUpdateAsync(
                row => row.SetProperty(one => one.ClaimedUntil, until),
                cancellationToken)
            .ConfigureAwait(false);

        return claimed == 1 ? new SendClaim(delivery, until) : null;
    }

    /// <inheritdoc/>
    public async ValueTask<SendDelivery?> FindAsync(
        SendDeliveryId delivery,
        CancellationToken cancellationToken)
    {
        SendDeliveryRecord? record = await context.SendOutbox
            .AsNoTracking()
            .SingleOrDefaultAsync(row => row.Id == delivery, cancellationToken)
            .ConfigureAwait(false);

        if (record is null)
        {
            return null;
        }

        byte[] deploymentKey = await deployment.UnwrappedAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            return Read(record, deploymentKey);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(deploymentKey);
        }
    }

    /// <inheritdoc/>
    public async ValueTask<bool> WaitsAsync(SendDeliveryId delivery, CancellationToken cancellationToken) =>
        await context.SendOutbox
            .AsNoTracking()
            .AnyAsync(row => row.Id == delivery, cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc/>
    public async ValueTask<byte[]?> ErasedAsync(SendDeliveryId delivery, CancellationToken cancellationToken)
    {
        var held = await context.SendOutbox
            .AsNoTracking()
            .Where(row => row.Id == delivery)
            .Select(row => new { row.WrappedKey, row.Reference })
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return held is not null && PersonalFieldCipher.IsErased(held.WrappedKey) ? held.Reference : null;
    }

    /// <inheritdoc/>
    public async ValueTask<bool> RecordAsync(
        SendDelivery delivery,
        SendClaim claim,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(delivery);

        return await context.SendOutbox
            .Where(row => row.Id == claim.Delivery && row.ClaimedUntil == claim.Until)
            .ExecuteUpdateAsync(
                row => row
                    .SetProperty(one => one.Attempts, delivery.Attempts)
                    .SetProperty(one => one.NextAttemptAt, delivery.NextAttemptAt)
                    .SetProperty(one => one.ClaimedUntil, (DateTimeOffset?)null),
                cancellationToken)
            .ConfigureAwait(false) == 1;
    }

    /// <inheritdoc/>
    public async ValueTask<bool> RemoveAsync(SendClaim claim, CancellationToken cancellationToken) =>
        await context.SendOutbox
            .Where(row => row.Id == claim.Delivery && row.ClaimedUntil == claim.Until)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false) == 1;

    /// <summary>
    /// The reference of a row written before a send carried one: the row's identifier,
    /// as the 128 bits it is, in base64url.
    /// </summary>
    /// <param name="delivery">What the row is held under.</param>
    /// <returns>The reference as text.</returns>
    public static string Unreferenced(SendDeliveryId delivery) =>
        Base64Url.EncodeToString(delivery.Value.ToByteArray(bigEndian: true));

    // A message concerning an account is bound to its subject, and one concerning no
    // account to its own row (PRIV-RIGHT-005a, D-173); the data key is the row's own
    // either way, so nothing else reads what this row holds.
    private static PersonalFieldLocation Located(SendDeliveryId delivery, SubjectId? subject) =>
        new(
            subject ?? new SubjectId(delivery.Value),
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

    private static SendDelivery Read(SendDeliveryRecord record, ReadOnlySpan<byte> deploymentKey)
    {
        byte[] dataKey = PersonalFieldCipher.Unwrap(record.WrappedKey, deploymentKey);

        SendDeliveryDocument document;

        try
        {
            document = JsonSerializer.Deserialize(
                PersonalFieldCipher.Decrypt(dataKey, Located(record.Id, record.Subject), record.Message),
                SendDeliveryJson.Default.SendDeliveryDocument)
                ?? throw new InvalidOperationException("The undelivered message is not a document.");
        }
        finally
        {
            CryptographicOperations.ZeroMemory(dataKey);
        }

        SendKind kind = VocabularyConverter<SendKind>.Read(document.Kind);

        var message = new OutboundMessage(
            Destination(kind, document.Destination),
            VocabularyConverter<MessageKind>.Read(document.Message),
            VocabularyConverter<RestrictionPurpose>.Read(document.Purpose),
            document.Source,
            document.Language)
        {
            Subject = document.Subject is Guid subject ? new SubjectId(subject) : null,
            Values = new Dictionary<string, string>(document.Values, StringComparer.Ordinal),
        };

        // A row written before a send carried its reference holds none in its content.
        // Its reference is made from the row's own identifier, the same at every read,
        // and the row's reference column holds that reference's hash, as the migration
        // that added the column wrote it. Nothing was counted under it, so the publisher
        // judges such a send before it is carried.
        string kept = document.Reference ?? Unreferenced(record.Id);

        SendReference reference = SendReference.TryParse(kept, out SendReference drawn)
            ? drawn
            : throw new InvalidOperationException("The stored reference is not a reference.");

        return new SendDelivery(record.Id, record.RecordedAt, message, reference)
        {
            Attempts = record.Attempts,
            NextAttemptAt = record.NextAttemptAt,
        };
    }

    private byte[] Written(ReadOnlySpan<byte> dataKey, SendDelivery delivery)
    {
        OutboundMessage message = delivery.Requested;

        var document = new SendDeliveryDocument(
            VocabularyConverter<SendKind>.Write(message.Kind),
            message.Destination.Canonical,
            VocabularyConverter<MessageKind>.Write(message.Message),
            VocabularyConverter<RestrictionPurpose>.Write(message.Purpose),
            message.Source,
            message.Language,
            message.Subject?.Value,
            message.Values,
            delivery.Reference.Value);

        return PersonalFieldCipher.Encrypt(
            dataKey,
            Located(delivery.Id, message.Subject),
            JsonSerializer.SerializeToUtf8Bytes(document, SendDeliveryJson.Default.SendDeliveryDocument),
            randomness);
    }
}
