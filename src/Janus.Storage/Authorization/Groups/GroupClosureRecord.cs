using System;
using Janus.Core;

namespace Janus.Storage.Authorization.Groups;

/// <summary>
/// The <c>group_closure</c> row: a subject and a group holding it at any depth, written
/// in the same transaction as the membership change it follows from.
/// </summary>
/// <remarks>
/// Implements AUTHZ-GROUP-001, AUTHZ-GROUP-002 and AUTHZ-INHERIT-002 AC4. Resolving a
/// principal's groups is one indexed read of this table, so no permission query walks
/// the nesting and none carries a recursive common table expression.
/// </remarks>
internal sealed class GroupClosureRecord
{
    /// <summary>
    /// The <c>group</c> column: the group holding the subject.
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

    /// <summary>
    /// The <c>depth</c> column: one where the group holds the subject directly.
    /// </summary>
    public int Depth { get; set; }
}
