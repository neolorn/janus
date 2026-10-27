using System;
using System.Globalization;
using Janus.Core;

namespace Janus.Authorization.Groups;

/// <summary>
/// The event a change of a group's members is announced by, wherever the change is made.
/// </summary>
/// <remarks>
/// Implements AUTHZ-GROUP-001, IDN-MEM-001 and chapter 10 section 5b (D-218). A member
/// that is an account is the event's subject; a group is no data subject.
/// </remarks>
internal static class GroupMembershipChanges
{
    /// <summary>
    /// The event announcing that a member joined or left a group.
    /// </summary>
    /// <param name="group">The group.</param>
    /// <param name="member">The account or the group added or taken out.</param>
    /// <param name="change">Which of the two happened.</param>
    /// <param name="at">When.</param>
    /// <returns>The event, naming no actor; the caller adds who acted, where a person did.</returns>
    /// <exception cref="ArgumentNullException">The group is absent.</exception>
    public static GroupMembershipChanged Of(
        Group group,
        GrantSubject member,
        GroupMembershipChange change,
        DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(group);

        string kind = change is GroupMembershipChange.Added ? "group-member-added" : "group-member-removed";

        return new GroupMembershipChanged(
            at,
            string.Create(CultureInfo.InvariantCulture, $"{kind}:{group.Id.Value}:{member.Value}@{at.UtcTicks}"),
            group.Id,
            group.Organization,
            member.Type,
            member.Value,
            change)
        {
            Subject = member.Type is SubjectType.User ? new SubjectId(member.Value) : null,
        };
    }
}
