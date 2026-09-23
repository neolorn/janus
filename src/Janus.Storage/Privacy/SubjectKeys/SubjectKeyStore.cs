using System;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Privacy.SubjectKeys;

namespace Janus.Storage.Privacy.SubjectKeys;

/// <summary>
/// Subject keys, over the <c>subject_keys</c> table.
/// </summary>
/// <param name="context">The context the operation's writes are tracked on.</param>
/// <param name="keyEncryptionKeys">The versions a subject key may be wrapped under.</param>
/// <param name="randomness">The randomness a data key is drawn from.</param>
/// <remarks>Implements PRIV-RIGHT-005a, OPS-SEC-003 and CONV-DESIGN-003.</remarks>
internal sealed class SubjectKeyStore(
    StoreContext context,
    KeyEncryptionKeys keyEncryptionKeys,
    RandomNumberGenerator randomness) : ISubjectKeyStore
{
    /// <inheritdoc/>
    public async ValueTask<SubjectKey?> FindBySubjectAsync(
        SubjectId subject,
        CancellationToken cancellationToken)
    {
        SubjectKeyRecord? record = await FindAsync(subject, cancellationToken).ConfigureAwait(false);

        return record is null ? null : SubjectKey.Existing(
            record.Subject,
            record.FormatMarker,
            record.KeyVersion,
            record.WrappedKey);
    }

    /// <inheritdoc/>
    public async ValueTask AddAsync(SubjectKey key, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(key);

        await context.SubjectKeys
            .AddAsync(
                new SubjectKeyRecord
                {
                    Subject = key.Subject,
                    FormatMarker = key.FormatMarker,
                    KeyVersion = key.KeyVersion,
                    WrappedKey = key.WrappedKey.ToArray(),
                },
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask CreateAsync(SubjectId subject, CancellationToken cancellationToken)
    {
        byte[] dataKey = PersonalFieldCipher.NewDataKey(randomness);

        try
        {
            await AddAsync(
                    SubjectKey.Wrapped(
                        subject,
                        keyEncryptionKeys.CurrentVersion,
                        PersonalFieldCipher.Wrap(dataKey, keyEncryptionKeys.Current.Span)),
                    cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(dataKey);
        }
    }

    /// <inheritdoc/>
    public async ValueTask RecordWrappingAsync(SubjectKey key, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(key);

        SubjectKeyRecord record = await FindAsync(key.Subject, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("The subject has no key row to carry the wrapping.");

        record.FormatMarker = key.FormatMarker;
        record.KeyVersion = key.KeyVersion;
        record.WrappedKey = key.WrappedKey.ToArray();
    }

    private async ValueTask<SubjectKeyRecord?> FindAsync(
        SubjectId subject,
        CancellationToken cancellationToken) =>
        await context.SubjectKeys.FindAsync([subject], cancellationToken).ConfigureAwait(false);
}
