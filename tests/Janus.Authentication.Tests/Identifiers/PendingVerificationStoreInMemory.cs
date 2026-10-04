using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Identifiers;
using Janus.Authentication.Registration;
using Janus.Core;

namespace Janus.Authentication.Tests.Identifiers;

/// <summary>
/// The verifications an account has outstanding, keyed as the table is: by the
/// identifier they belong to, and found by the link a message carried. What is read is
/// a copy, as a row read is, so a change reaches the store only when it is recorded.
/// </summary>
internal sealed class PendingVerificationStoreInMemory : IPendingVerificationStore
{
    private readonly Dictionary<IdentifierId, PendingVerification> _pending = [];

    /// <summary>
    /// The unit of work the operations under test run in. Where a test names it, what
    /// the store wrote inside a unit of work that rolled back is put back.
    /// </summary>
    public UnitOfWorkInMemory? Work { get; set; }

    /// <summary>
    /// Every verification the store holds.
    /// </summary>
    public IReadOnlyCollection<PendingVerification> All => _pending.Values;

    /// <summary>
    /// What another transaction committed on a verification while this one waited for
    /// its lock, applied as the lock is taken.
    /// </summary>
    public Action<IdentifierId>? Locking { get; set; }

    /// <inheritdoc/>
    public ValueTask<PendingVerification?> FindAsync(
        IdentifierId identifier,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult(_pending.TryGetValue(identifier, out PendingVerification? held) ? Copied(held) : null);

    /// <inheritdoc/>
    public ValueTask<PendingVerification?> FindForUpdateAsync(
        IdentifierId identifier,
        CancellationToken cancellationToken)
    {
        Locking?.Invoke(identifier);

        return FindAsync(identifier, cancellationToken);
    }

    /// <inheritdoc/>
    public ValueTask<PendingVerification?> FindByLinkAsync(
        byte[] fingerprint,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult(_pending.Values
            .Where(pending =>
                (pending.Staged.Link is byte[] link && link.SequenceEqual(fingerprint))
                || (pending.OldLink is byte[] old && old.SequenceEqual(fingerprint)))
            .Select(Copied)
            .FirstOrDefault());

    /// <inheritdoc/>
    public ValueTask<IReadOnlyList<PendingVerification>> AddsOfAsync(
        SubjectId subject,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult<IReadOnlyList<PendingVerification>>([.. _pending.Values
            .Where(pending => pending.Subject == subject && !pending.IsReplacement)
            .OrderBy(pending => pending.StagedAt)
            .ThenBy(pending => pending.Identifier.Value)
            .Select(Copied)]);

    /// <inheritdoc/>
    public ValueTask AddAsync(PendingVerification pending, CancellationToken cancellationToken)
    {
        Enlist();

        _pending[pending.Identifier] = Copied(pending);

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask RecordAsync(PendingVerification pending, CancellationToken cancellationToken)
    {
        Enlist();

        _pending[pending.Identifier] = Copied(pending);

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask RemoveAsync(IdentifierId identifier, CancellationToken cancellationToken)
    {
        Enlist();

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

    private static PendingVerification Copied(PendingVerification pending) =>
        PendingVerification.Existing(
            pending.Subject,
            pending.Browser,
            StagedIdentity.Existing(
                pending.Staged.Id,
                pending.Staged.Kind,
                pending.Staged.Entered,
                pending.Staged.Canonical,
                pending.Staged.IsLocked,
                pending.Staged.IsExtra,
                pending.Staged.Link,
                pending.Staged.VerifiedAt),
            pending.IsReplacement,
            pending.OldMustConfirm,
            pending.OldConfirmedAt,
            pending.OldLink,
            pending.StagedAt);

    private void Enlist()
    {
        if (Work is null)
        {
            return;
        }

        var held = new Dictionary<IdentifierId, PendingVerification>(_pending);

        Work.Undoing(() =>
        {
            _pending.Clear();

            foreach (KeyValuePair<IdentifierId, PendingVerification> pending in held)
            {
                _pending[pending.Key] = pending.Value;
            }
        });
    }
}
