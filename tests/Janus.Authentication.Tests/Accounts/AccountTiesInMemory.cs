using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Accounts;
using Janus.Core;

namespace Janus.Authentication.Tests.Accounts;

/// <summary>
/// Which accounts each organization reaches, held in memory.
/// </summary>
/// <remarks>Implements IDN-READ-004 for the tests.</remarks>
internal sealed class AccountTiesInMemory : IAccountTies
{
    private readonly HashSet<(SubjectId Subject, OrganizationId Organization)> _tied = [];

    /// <summary>
    /// Ties an account to an organization, as a membership or a registered record does.
    /// </summary>
    /// <param name="subject">Whose account.</param>
    /// <param name="organization">Which organization.</param>
    public void Tie(SubjectId subject, OrganizationId organization) => _tied.Add((subject, organization));

    /// <inheritdoc/>
    public ValueTask<bool> TiedAsync(
        SubjectId subject,
        OrganizationId organization,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult(_tied.Contains((subject, organization)));
}
