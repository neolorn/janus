using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Privacy.SubjectKeys;
using Microsoft.EntityFrameworkCore;

namespace Janus.Storage.Privacy.SubjectKeys;

/// <summary>
/// Subject keys, over the <c>subject_keys</c> table.
/// </summary>
/// <param name="context">The context the operation's writes are tracked on.</param>
/// <param name="ring">The key ring the keys are borrowed from at each use.</param>
/// <param name="randomness">The randomness a data key is drawn from.</param>
/// <remarks>Implements PRIV-RIGHT-005a, OPS-SEC-003 and CONV-DESIGN-003.</remarks>
internal sealed class SubjectKeyStore(
    StoreContext context,
    IKeyRing ring,
    RandomNumberGenerator randomness) : ISubjectKeyStore
{
    /// <inheritdoc/>
    public async ValueTask<SubjectKey?> FindBySubjectAsync(
        SubjectId subject,
        CancellationToken cancellationToken)
    {
        SubjectKeyRecord? record = await FindAsync(SubjectKeyId.Of(subject), cancellationToken).ConfigureAwait(false);

        return record is null ? null : SubjectKey.Existing(
            record.Id,
            record.FormatMarker,
            record.KeyVersion,
            record.WrappedKey);
    }

    /// <inheritdoc/>
    public async ValueTask<IReadOnlySet<int>> WrappingVersionsAsync(CancellationToken cancellationToken) =>
        (await context.SubjectKeys
            .Where(key => key.FormatMarker != PersonalDataFormat.ErasedMarker)
            .Select(key => key.KeyVersion)
            .Distinct()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false))
        .ToHashSet();

    /// <inheritdoc/>
    public async ValueTask AddAsync(SubjectKey key, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(key);

        await context.SubjectKeys
            .AddAsync(
                new SubjectKeyRecord
                {
                    Id = key.Id,
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
            (int version, byte[] wrapped) = PersonalFieldCipher.WrapUnderCurrent(dataKey, ring);

            await AddAsync(SubjectKey.Wrapped(SubjectKeyId.Of(subject), version, wrapped), cancellationToken)
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

        SubjectKeyRecord record = await FindAsync(key.Id, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("The subject has no key row to carry the wrapping.");

        record.FormatMarker = key.FormatMarker;
        record.KeyVersion = key.KeyVersion;
        record.WrappedKey = key.WrappedKey.ToArray();
    }

    private async ValueTask<SubjectKeyRecord?> FindAsync(
        SubjectKeyId id,
        CancellationToken cancellationToken) =>
        await context.SubjectKeys.FindAsync([id], cancellationToken).ConfigureAwait(false);
}
