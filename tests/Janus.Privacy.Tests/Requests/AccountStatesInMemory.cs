using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Privacy.Erasures;
using Janus.Privacy.Requests;

namespace Janus.Privacy.Tests.Requests;

/// <summary>
/// The account states the area moves, in a dictionary.
/// </summary>
internal sealed class AccountStatesInMemory : IAccountStates
{
    private readonly Dictionary<SubjectId, AccountState> _states = [];
    private readonly Dictionary<SubjectId, PendingDeletion> _deletions = [];
    private readonly Dictionary<SubjectId, DateTimeOffset> _sessionsEnded = [];

    /// <summary>
    /// Puts an account in a state, as a deployment has one.
    /// </summary>
    /// <param name="subject">Whose.</param>
    /// <param name="state">Which state.</param>
    public void Hold(SubjectId subject, AccountState state) => _states[subject] = state;

    /// <summary>
    /// What state the account is in.
    /// </summary>
    /// <param name="subject">Whose.</param>
    /// <returns>The state, or nothing where no account is held.</returns>
    public AccountState? Of(SubjectId subject) =>
        _states.TryGetValue(subject, out AccountState state) ? state : null;

    /// <inheritdoc/>
    public ValueTask<bool> RestrictAsync(SubjectId subject, CancellationToken cancellationToken) =>
        ValueTask.FromResult(Moved(subject, AccountState.Active, AccountState.Restricted));

    /// <inheritdoc/>
    public ValueTask<bool> BeginDeletionAsync(
        SubjectId subject,
        DeletionOrigin origin,
        DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        bool moved = Moved(subject, AccountState.Active, AccountState.Deleting)
            || Moved(subject, AccountState.Restricted, AccountState.Deleting);

        if (moved)
        {
            Deleting = origin;
            _deletions[subject] = new PendingDeletion(subject, origin, at);
        }

        return ValueTask.FromResult(moved);
    }

    /// <inheritdoc/>
    public ValueTask<IReadOnlyList<PendingDeletion>> DeletingSinceAsync(
        DateTimeOffset before,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult<IReadOnlyList<PendingDeletion>>(
        [
            .. _deletions.Values
                .Where(deletion => deletion.Since <= before
                    && Of(deletion.Subject) is AccountState.Deleting)
                .OrderBy(deletion => deletion.Since),
        ]);

    /// <summary>
    /// Puts an account in a grace window somebody else began, as a takedown or the
    /// privacy queue does.
    /// </summary>
    /// <param name="subject">Whose.</param>
    /// <param name="origin">What began it.</param>
    /// <param name="since">When the window began.</param>
    public void Deletes(SubjectId subject, DeletionOrigin origin, DateTimeOffset since)
    {
        _states[subject] = AccountState.Deleting;
        _deletions[subject] = new PendingDeletion(subject, origin, since);
    }

    /// <summary>
    /// Takes an account out of the deletion window, as the subject's cancellation does.
    /// </summary>
    /// <param name="subject">Whose.</param>
    public void Cancels(SubjectId subject)
    {
        _states[subject] = AccountState.Active;
        _ = _deletions.Remove(subject);
    }

    /// <summary>
    /// Marks an account erased, as the sweep does at the end of its window.
    /// </summary>
    /// <param name="subject">Whose.</param>
    public void Erases(SubjectId subject) => _states[subject] = AccountState.Deleted;

    /// <summary>
    /// When every session of the account was ended by a transition, where one was.
    /// </summary>
    /// <param name="subject">Whose.</param>
    /// <returns>The instant, or nothing.</returns>
    public DateTimeOffset? SessionsEndedAt(SubjectId subject) =>
        _sessionsEnded.TryGetValue(subject, out DateTimeOffset at) ? at : null;

    /// <inheritdoc/>
    public ValueTask<AccountStanding?> StandingAsync(
        SubjectId subject,
        CancellationToken cancellationToken)
    {
        if (!_states.TryGetValue(subject, out AccountState state))
        {
            return ValueTask.FromResult<AccountStanding?>(null);
        }

        PendingDeletion? deletion = _deletions.GetValueOrDefault(subject);

        return ValueTask.FromResult<AccountStanding?>(
            new AccountStanding(state, deletion?.By, deletion?.Since));
    }

    /// <inheritdoc/>
    public ValueTask<bool> TakeDownAsync(
        SubjectId subject,
        DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        if (Of(subject) is not (AccountState.Active or AccountState.Restricted or AccountState.Suspended))
        {
            return ValueTask.FromResult(false);
        }

        Deletes(subject, DeletionOrigin.Takedown, at);
        Deleting = DeletionOrigin.Takedown;
        _sessionsEnded[subject] = at;

        return ValueTask.FromResult(true);
    }

    /// <inheritdoc/>
    public ValueTask<bool> ReverseTakedownAsync(SubjectId subject, CancellationToken cancellationToken)
    {
        if (Of(subject) is not AccountState.Deleting
            || _deletions.GetValueOrDefault(subject)?.By is not DeletionOrigin.Takedown)
        {
            return ValueTask.FromResult(false);
        }

        Cancels(subject);

        return ValueTask.FromResult(true);
    }

    /// <summary>
    /// What started the deletion, where one was started.
    /// </summary>
    public DeletionOrigin? Deleting { get; private set; }

    private bool Moved(SubjectId subject, AccountState from, AccountState to)
    {
        if (!_states.TryGetValue(subject, out AccountState state) || state != from)
        {
            return false;
        }

        _states[subject] = to;

        return true;
    }
}
