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
    /// Restricts the account's processing.
    /// </summary>
    /// <param name="subject">The account.</param>
    public void Restrict(SubjectId subject) => _restricted.Add(subject);

    /// <inheritdoc/>
    public ValueTask<Error?> RefusedAsync(SubjectId subject, CancellationToken cancellationToken) =>
        ValueTask.FromResult(_restricted.Contains(subject) ? Error.From(ErrorCodes.Restricted) : null);
}
