using System;
using System.Collections.Generic;
using System.Text.Json;
using Janus.Core;

namespace Janus.Identity.Organizations;

/// <summary>
/// An account's membership of an organization: a record of its own, with a lifecycle
/// that belongs neither to the account nor to the organization.
/// </summary>
/// <remarks>
/// Implements IDN-MEM-001, IDN-MEM-002, IDN-PRIN-003 and REG-INV-001. Ending a
/// membership leaves both sides where they are and removes no row: the end is an
/// instant on the record. A membership an invitation attached carries what the person
/// acknowledged for it.
/// How many memberships an account may hold at once is
/// <c>organization.multiplememberships</c>, which the caller reads and this type
/// applies: the schema takes any number and the rule lives here.
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
    /// What the person acknowledged when an invitation attached it, where one did.
    /// </summary>
    public MembershipAcknowledgement? Acknowledgement { get; private init; }

    /// <summary>
    /// Whether the membership is running: it began and has not ended.
    /// </summary>
    public bool IsCurrent => EndedAt is null;

    /// <summary>
    /// A new membership, where the account may hold one more.
    /// </summary>
    /// <param name="id">The identifier issued for it.</param>
    /// <param name="subject">Whose it is.</param>
    /// <param name="organization">Which organization it is of.</param>
    /// <param name="held">
    /// Every membership the account already holds, ended ones included.
    /// </param>
    /// <param name="multiple">What <c>organization.multiplememberships</c> allows.</param>
    /// <param name="createdAt">When it begins.</param>
    /// <param name="acknowledgement">
    /// What the person acknowledged, where an invitation attaches it.
    /// </param>
    /// <returns>The membership, or the refusal and its code.</returns>
    /// <exception cref="ArgumentNullException">The memberships held are absent.</exception>
    /// <exception cref="ArgumentException">One of them belongs to another account.</exception>
    public static Result<Membership> Create(
        MembershipId id,
        SubjectId subject,
        OrganizationId organization,
        IEnumerable<Membership> held,
        bool multiple,
        DateTimeOffset createdAt,
        MembershipAcknowledgement? acknowledgement = null)
    {
        ArgumentNullException.ThrowIfNull(held);

        int current = 0;

        foreach (Membership membership in held)
        {
            if (membership.Subject != subject)
            {
                throw new ArgumentException(
                    "The membership belongs to another account.",
                    nameof(held));
            }

            if (!membership.IsCurrent)
            {
                continue;
            }

            current++;

            if (membership.Organization == organization)
            {
                return Result.Failure<Membership>(Error.From(
                    ErrorCodes.MembershipLimitReached,
                    "organization",
                    JsonSerializer.SerializeToElement(organization.Value)));
            }
        }

        return current is not 0 && !multiple
            ? Result.Failure<Membership>(Error.From(ErrorCodes.MembershipLimitReached))
            : Result.Success(new Membership(id, subject, organization, createdAt)
            {
                Acknowledgement = acknowledgement,
            });
    }

    /// <summary>
    /// The membership as it already stands. This is the store's translation of a
    /// stored row and no change an administrator made.
    /// </summary>
    /// <param name="id">The identifier.</param>
    /// <param name="subject">Whose it is.</param>
    /// <param name="organization">Which organization it is of.</param>
    /// <param name="createdAt">When it began.</param>
    /// <param name="endedAt">When it ended, where it has.</param>
    /// <param name="acknowledgement">What the person acknowledged, where an invitation attached it.</param>
    /// <returns>The membership.</returns>
    public static Membership Existing(
        MembershipId id,
        SubjectId subject,
        OrganizationId organization,
        DateTimeOffset createdAt,
        DateTimeOffset? endedAt,
        MembershipAcknowledgement? acknowledgement) =>
        new(id, subject, organization, createdAt) { EndedAt = endedAt, Acknowledgement = acknowledgement };

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
