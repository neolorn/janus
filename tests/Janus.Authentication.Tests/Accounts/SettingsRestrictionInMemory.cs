using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Accounts;
using Janus.Core;

namespace Janus.Authentication.Tests.Accounts;

/// <summary>
/// The gate's answer on an account's own settings, held in memory: an account a test
/// restricts is refused as the gate refuses it, and every other one is not.
/// </summary>
internal sealed class SettingsRestrictionInMemory : ISettingsRestriction
{
    private readonly HashSet<SubjectId> _restricted = [];

    /// <summary>
    /// Gets or sets what happens once a change of an account's own settings is next
    /// admitted, where a test sets it: what follows an operation's gate step. It happens
    /// once, and is handed the account admitted.
    /// </summary>
    public Action<SubjectId>? Admitted { get; set; }

    /// <summary>
    /// Restricts the account's processing.
    /// </summary>
    /// <param name="subject">The account.</param>
    public void Restrict(SubjectId subject) => _restricted.Add(subject);

    /// <summary>
    /// Lifts the restriction.
    /// </summary>
    /// <param name="subject">The account.</param>
    public void Lift(SubjectId subject) => _restricted.Remove(subject);

    /// <inheritdoc/>
    public ValueTask<Error?> RefusedAsync(AccessContext context, CancellationToken cancellationToken)
    {
        if (context?.Effective is not SubjectId subject)
        {
            return ValueTask.FromResult<Error?>(null);
        }

        if (_restricted.Contains(subject))
        {
            return ValueTask.FromResult<Error?>(Error.From(ErrorCodes.Restricted));
        }

        if (Admitted is Action<SubjectId> admitted)
        {
            Admitted = null;
            admitted(subject);
        }

        return ValueTask.FromResult<Error?>(null);
    }
}
