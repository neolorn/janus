using System;
using Janus.Core;

namespace Janus.Storage.Authorization.Groups;

/// <summary>
/// The <c>group_members</c> row: one account or one group held directly by a group.
/// </summary>
/// <remarks>
/// Implements AUTHZ-GROUP-001 and CONV-DESIGN-003. Nothing in the shape fixes how deep
/// the nesting goes, because a member is itself either kind of subject.
/// </remarks>
internal sealed class GroupMemberRecord
{
    /// <summary>
    /// The <c>group</c> column.
    /// </summary>
    public GroupId Group { get; set; }

    /// <summary>
    /// The <c>member_type</c> column.
    /// </summary>
    public SubjectType MemberType { get; set; }

    /// <summary>
    /// The <c>member_id</c> column.
    /// </summary>
    public Guid MemberId { get; set; }
}
