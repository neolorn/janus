using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Organizations;
using Janus.Authentication.Tests.Policies;
using Janus.Core;

namespace Janus.Authentication.Tests.Organizations;

/// <summary>
/// The organizations of a deployment held in memory, their members read from the
/// memberships a test placed.
/// </summary>
/// <param name="memberships">Where the members of each organization are read.</param>
/// <remarks>
/// CONV-TEST-004: a fake that keeps the window the way the organization does, refusing
/// the administrative one, rather than answering whatever a test told it to.
/// </remarks>
internal sealed class OrganizationsInMemory(MembershipLookupInMemory memberships) : IOrganizationDirectory
{
    private readonly Dictionary<OrganizationId, OrganizationStanding> _held = [];
    private readonly Dictionary<OrganizationId, string> _names = [];

    /// <summary>
    /// Writes an organization as bootstrap or an earlier operation left it.
    /// </summary>
    /// <param name="organization">The organization.</param>
    /// <param name="administrative">Whether it is the administrative one.</param>
    /// <param name="deletionRequestedAt">When its deletion was requested, where it was.</param>
    /// <param name="erasedAt">When it was erased, where it was.</param>
    public void Seed(
        OrganizationId organization,
        bool administrative = false,
        DateTimeOffset? deletionRequestedAt = null,
        DateTimeOffset? erasedAt = null)
    {
        _held[organization] = new OrganizationStanding(organization, administrative, deletionRequestedAt, erasedAt);
        _names[organization] = organization.ToString();
    }

    /// <summary>
    /// What an organization is called.
    /// </summary>
    /// <param name="organization">The organization.</param>
    /// <returns>Its name, or nothing where none is held.</returns>
    public string? NameOf(OrganizationId organization) => _names.GetValueOrDefault(organization);

    /// <inheritdoc/>
    public ValueTask<OrganizationStanding?> FindAsync(
        OrganizationId organization,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult(_held.GetValueOrDefault(organization));

    /// <inheritdoc/>
    public ValueTask CreateAsync(
        OrganizationId organization,
        string name,
        DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        Seed(organization);
        _names[organization] = name;

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask<Result> RequestDeletionAsync(
        OrganizationId organization,
        DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        OrganizationStanding held = _held[organization];

        if (held.IsAdministrative)
        {
            return ValueTask.FromResult(Result.Failure(Error.From(ErrorCodes.OrganizationProtected)));
        }

        _held[organization] = held with { DeletionRequestedAt = at };

        return ValueTask.FromResult(Result.Success());
    }

    /// <inheritdoc/>
    public ValueTask CancelDeletionAsync(OrganizationId organization, CancellationToken cancellationToken)
    {
        _held[organization] = _held[organization] with { DeletionRequestedAt = null };

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask<IReadOnlyList<SubjectId>> MembersAsync(
        OrganizationId organization,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult(memberships.Members(organization));
}
