using System;
using Janus.Core;

namespace Janus.Hosting.Authorization;

/// <summary>
/// What adding a member to a group, or taking one out, carries, as the request reads.
/// </summary>
/// <param name="SubjectType">Whether the member is one account or a group.</param>
/// <param name="SubjectId">The account or the group.</param>
/// <param name="Reason">Why, which every change to a group records.</param>
/// <remarks>Implements chapter 09 section 8a and AUTHZ-GROUP-001.</remarks>
internal sealed record MemberBody(SubjectType? SubjectType, Guid? SubjectId, string? Reason)
{
    /// <summary>
    /// The member the body names, or the member of the body it cannot be read at.
    /// </summary>
    /// <returns>The member, or nothing and the field that stopped it.</returns>
    public (GrantSubject? Subject, string Member) Read()
    {
        if (SubjectType is not Core.SubjectType type)
        {
            return (null, "subjectType");
        }

        if (SubjectId is not Guid subject)
        {
            return (null, "subjectId");
        }

        return (new GrantSubject(type, subject), string.Empty);
    }
}
