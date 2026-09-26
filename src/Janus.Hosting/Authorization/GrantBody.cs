using System;
using Janus.Core;

namespace Janus.Hosting.Authorization;

/// <summary>
/// What writing a grant carries, as the request reads.
/// </summary>
/// <param name="SubjectType">Whether it is given to one account or to a group.</param>
/// <param name="SubjectId">The account or the group.</param>
/// <param name="ResourceType">The kind of thing it is on, <c>organization</c> for the whole organization.</param>
/// <param name="ResourceId">The record it is on, or the organization's identifier.</param>
/// <param name="Role">The role it confers or denies.</param>
/// <param name="Deny">Whether it takes access away; an allow where absent.</param>
/// <param name="ExpiresAt">When it stops conferring anything, where it expires.</param>
/// <param name="Reason">Why, which every grant records.</param>
/// <remarks>Implements chapter 09 section 8, AUTHZ-GRANT-001 and AUTHZ-GRANT-003.</remarks>
internal sealed record GrantBody(
    SubjectType? SubjectType,
    Guid? SubjectId,
    string? ResourceType,
    string? ResourceId,
    string? Role,
    bool? Deny,
    DateTimeOffset? ExpiresAt,
    string? Reason)
{
    /// <summary>
    /// The grant the body describes, or the member it cannot be read at.
    /// </summary>
    /// <param name="reason">
    /// The body's reason, which the endpoint has found present, since chapter 10 names
    /// its own refusal for a grant without one.
    /// </param>
    /// <returns>The grant, or nothing and the member that stopped it.</returns>
    public (GrantRequest? Request, string Member) Read(string reason)
    {
        if (SubjectType is not Core.SubjectType type)
        {
            return (null, "subjectType");
        }

        if (SubjectId is not Guid subject)
        {
            return (null, "subjectId");
        }

        if (!Core.ResourceType.TryParse(ResourceType, out Core.ResourceType on))
        {
            return (null, "resourceType");
        }

        if (string.IsNullOrWhiteSpace(ResourceId))
        {
            return (null, "resourceId");
        }

        if (!RoleName.TryParse(Role, out RoleName role))
        {
            return (null, "role");
        }

        return (
            new GrantRequest(
                new GrantSubject(type, subject),
                role,
                new ResourceReference(on, Core.ResourceId.Parse(ResourceId)),
                Deny ?? false,
                ExpiresAt,
                reason),
            string.Empty);
    }
}
