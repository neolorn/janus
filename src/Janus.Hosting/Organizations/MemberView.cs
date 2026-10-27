using System;
using Janus.Core;

namespace Janus.Hosting.Organizations;

/// <summary>
/// One current member of an organization, as a read of it and the list carry it.
/// </summary>
/// <param name="Subject">The member's account.</param>
/// <param name="Membership">The membership's identifier.</param>
/// <param name="BeganAt">When the membership began.</param>
/// <param name="EndsAt">The end set in advance, or nothing.</param>
/// <param name="Invitation">The invitation whose acknowledgement began it, or nothing.</param>
/// <remarks>Implements chapter 09 section 8a, IDN-READ-002 and API-CONV-002.</remarks>
internal sealed record MemberView(
    Guid Subject,
    Guid Membership,
    DateTimeOffset BeganAt,
    DateTimeOffset? EndsAt,
    Guid? Invitation)
{
    /// <summary>
    /// The view of one member.
    /// </summary>
    /// <param name="member">The member.</param>
    /// <returns>The view.</returns>
    /// <exception cref="ArgumentNullException">The member is absent.</exception>
    public static MemberView Of(MemberSummary member)
    {
        ArgumentNullException.ThrowIfNull(member);

        return new(
            member.Subject.Value,
            member.Membership.Value,
            member.BeganAt,
            member.EndsAt,
            member.Invitation?.Value);
    }
}
