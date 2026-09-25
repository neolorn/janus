using System;
using Janus.Core;

namespace Janus.Hosting.Authorization;

/// <summary>
/// A live grant one user or group holds in its own name.
/// </summary>
/// <param name="Id">The grant's identifier, which a revocation names.</param>
/// <param name="Kind">Whether somebody wrote it or a materialisation did.</param>
/// <param name="SubjectType">Whether it names a user or a group.</param>
/// <param name="SubjectId">The user or group it names.</param>
/// <param name="ResourceType">The type of the record it is on, <c>organization</c> for the whole organization.</param>
/// <param name="ResourceId">The record it is on, or the organization's identifier.</param>
/// <param name="Role">The role it confers or denies.</param>
/// <param name="Deny">Whether it takes access away.</param>
/// <param name="ExpiresAt">When it stops conferring anything, where it expires.</param>
/// <param name="GrantedBy">Who wrote it.</param>
/// <param name="GrantedAt">When it was written.</param>
/// <param name="Reason">Why it was written.</param>
/// <remarks>Implements AUTHZ-GRANT-003 AC3 in the shape a grant is written in (entry 268).</remarks>
internal sealed record HeldGrantView(
    Guid Id,
    GrantKind Kind,
    SubjectType SubjectType,
    Guid SubjectId,
    string ResourceType,
    string ResourceId,
    string Role,
    bool Deny,
    DateTimeOffset? ExpiresAt,
    Guid GrantedBy,
    DateTimeOffset GrantedAt,
    string Reason)
{
    /// <summary>
    /// The view of a held grant.
    /// </summary>
    /// <param name="grant">The grant.</param>
    /// <returns>The view.</returns>
    /// <exception cref="ArgumentNullException">The grant is absent.</exception>
    public static HeldGrantView Of(HeldGrant grant)
    {
        ArgumentNullException.ThrowIfNull(grant);

        return new HeldGrantView(
            grant.Id.Value,
            grant.Kind,
            grant.Subject.Type,
            grant.Subject.Value,
            grant.On.Type.ToString(),
            grant.On.Id.ToString(),
            grant.Role.ToString(),
            grant.Deny,
            grant.ExpiresAt,
            grant.GrantedBy.Value,
            grant.GrantedAt,
            grant.Reason);
    }
}
