using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Identifiers;
using Janus.Core;

namespace Janus.Authentication.Tests.Identifiers;

/// <summary>
/// The verifications an account has outstanding, keyed as the table is: by the
/// identifier they belong to, and found by the link a message carried.
/// </summary>
internal sealed class PendingVerificationStoreInMemory : IPendingVerificationStore
{
    private readonly Dictionary<IdentifierId, PendingVerification> _pending = [];

    /// <summary>
    /// Every verification the store holds.
    /// </summary>
    public IReadOnlyCollection<PendingVerification> All => _pending.Values;

    /// <inheritdoc/>
    public ValueTask<PendingVerification?> FindAsync(
        IdentifierId identifier,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult(_pending.GetValueOrDefault(identifier));

    /// <inheritdoc/>
    public ValueTask<PendingVerification?> FindByLinkAsync(
        byte[] fingerprint,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult(_pending.Values.FirstOrDefault(pending =>
            (pending.Staged.Link is byte[] link && link.SequenceEqual(fingerprint))
            || (pending.OldLink is byte[] old && old.SequenceEqual(fingerprint))));

    /// <inheritdoc/>
    public ValueTask AddAsync(PendingVerification pending, CancellationToken cancellationToken)
    {
        _pending[pending.Identifier] = pending;

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask RecordAsync(PendingVerification pending, CancellationToken cancellationToken)
    {
        _pending[pending.Identifier] = pending;

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask RemoveAsync(IdentifierId identifier, CancellationToken cancellationToken)
    {
        _ = _pending.Remove(identifier);

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask<int> SweepAsync(DateTimeOffset before, CancellationToken cancellationToken)
    {
        List<IdentifierId> gone = [.. _pending
            .Where(pending => pending.Value.StagedAt < before)
            .Select(pending => pending.Key)];

        foreach (IdentifierId identifier in gone)
        {
            _ = _pending.Remove(identifier);
        }

        return ValueTask.FromResult(gone.Count);
    }
}
