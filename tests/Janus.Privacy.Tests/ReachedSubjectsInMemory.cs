using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Privacy.Policies;

namespace Janus.Privacy.Tests;

/// <summary>
/// The organizations a read may be asked in, and whom each reaches, held in memory.
/// </summary>
/// <remarks>Implements IDN-READ-004 for the tests.</remarks>
internal sealed class ReachedSubjectsInMemory : IReachedSubjects
{
    private readonly HashSet<OrganizationId> _held = [];
    private readonly HashSet<(SubjectId Subject, OrganizationId Organization)> _tied = [];

    /// <summary>
    /// Holds an organization the deployment has not erased.
    /// </summary>
    /// <param name="organization">Which organization.</param>
    public void Hold(OrganizationId organization) => _held.Add(organization);

    /// <summary>
    /// Ties a person to an organization, as a membership or a registered record does.
    /// </summary>
    /// <param name="subject">Who.</param>
    /// <param name="organization">Which organization.</param>
    public void Tie(SubjectId subject, OrganizationId organization) => _tied.Add((subject, organization));

    /// <inheritdoc/>
    public ValueTask<bool> HeldAsync(OrganizationId organization, CancellationToken cancellationToken) =>
        ValueTask.FromResult(_held.Contains(organization));

    /// <inheritdoc/>
    public ValueTask<bool> TiedAsync(
        SubjectId subject,
        OrganizationId organization,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult(_tied.Contains((subject, organization)));
}
