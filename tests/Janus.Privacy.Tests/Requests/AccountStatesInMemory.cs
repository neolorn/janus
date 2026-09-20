using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Privacy.Requests;

namespace Janus.Privacy.Tests.Requests;

/// <summary>
/// The account states the area moves, in a dictionary.
/// </summary>
internal sealed class AccountStatesInMemory : IAccountStates
{
    private readonly Dictionary<SubjectId, AccountState> _states = [];

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
        }

        return ValueTask.FromResult(moved);
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
