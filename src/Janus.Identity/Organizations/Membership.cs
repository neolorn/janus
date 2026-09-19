using System;
using Janus.Core;

namespace Janus.Identity.Organizations;

/// <summary>
/// An account's membership of an organization: a record of its own, with a lifecycle
/// that belongs neither to the account nor to the organization.
/// </summary>
/// <remarks>
/// Implements IDN-MEM-001, IDN-MEM-002 and IDN-PRIN-003. Ending a membership leaves
/// both sides where they are and removes no row: the end is an instant on the record.
/// </remarks>
internal sealed class Membership
{
    private Membership(
        MembershipId id,
        SubjectId subject,
        OrganizationId organization,
        DateTimeOffset createdAt)
    {
        Id = id;
        Subject = subject;
        Organization = organization;
        CreatedAt = createdAt;
    }

    /// <summary>
    /// The membership's own identifier.
    /// </summary>
    public MembershipId Id { get; }

    /// <summary>
    /// Whose membership it is.
    /// </summary>
    public SubjectId Subject { get; }

    /// <summary>
    /// Which organization it is of.
    /// </summary>
    public OrganizationId Organization { get; }

    /// <summary>
    /// When the membership began.
    /// </summary>
    public DateTimeOffset CreatedAt { get; }

    /// <summary>
    /// When it ended, where it has.
    /// </summary>
    public DateTimeOffset? EndedAt { get; private set; }

    /// <summary>
    /// Whether the membership is running: it began and has not ended.
    /// </summary>
    public bool IsCurrent => EndedAt is null;

    /// <summary>
    /// A new membership.
    /// </summary>
    /// <param name="id">The identifier issued for it.</param>
    /// <param name="subject">Whose it is.</param>
    /// <param name="organization">Which organization it is of.</param>
    /// <param name="createdAt">When it begins.</param>
    /// <returns>The membership.</returns>
    public static Membership Create(
        MembershipId id,
        SubjectId subject,
        OrganizationId organization,
        DateTimeOffset createdAt) =>
        new(id, subject, organization, createdAt);

    /// <summary>
    /// The membership as it already stands. This is the store's translation of a
    /// stored row and no change an administrator made.
    /// </summary>
    /// <param name="id">The identifier.</param>
    /// <param name="subject">Whose it is.</param>
    /// <param name="organization">Which organization it is of.</param>
    /// <param name="createdAt">When it began.</param>
    /// <param name="endedAt">When it ended, where it has.</param>
    /// <returns>The membership.</returns>
    public static Membership Existing(
        MembershipId id,
        SubjectId subject,
        OrganizationId organization,
        DateTimeOffset createdAt,
        DateTimeOffset? endedAt) =>
        new(id, subject, organization, createdAt) { EndedAt = endedAt };

    /// <summary>
    /// Ends the membership. The account and the organization persist, and the record
    /// stays where it is.
    /// </summary>
    /// <param name="at">The instant it ends.</param>
    /// <exception cref="InvalidOperationException">The membership has already ended.</exception>
    public void End(DateTimeOffset at)
    {
        if (EndedAt is not null)
        {
            throw new InvalidOperationException("The membership has already ended.");
        }

        EndedAt = at;
    }
}
