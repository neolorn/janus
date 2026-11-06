using System;
using Janus.Core;

namespace Janus.Hosting.Accounts;

/// <summary>
/// A live grant the caller's own account holds in its own name, as the subject export
/// carries it.
/// </summary>
/// <param name="Id">The grant's identifier.</param>
/// <param name="Kind">Whether somebody wrote it or a materialisation did.</param>
/// <param name="ResourceType">The type of the record it is on, <c>organization</c> for the whole organization.</param>
/// <param name="ResourceId">The record it is on, or the organization's identifier.</param>
/// <param name="Role">The role it confers or denies.</param>
/// <param name="Deny">Whether it takes access away.</param>
/// <param name="ExpiresAt">When it stops conferring anything, where it expires.</param>
/// <param name="GrantedAt">When it was written.</param>
/// <remarks>Implements AUTHZ-GRANT-003 criterion 9 (D-274): never who granted it or why.</remarks>
internal sealed record OwnGrantView(
    Guid Id,
    GrantKind Kind,
    string ResourceType,
    string ResourceId,
    string Role,
    bool Deny,
    DateTimeOffset? ExpiresAt,
    DateTimeOffset GrantedAt)
{
    /// <summary>
    /// The view of one of the caller's own grants.
    /// </summary>
    /// <param name="grant">The grant.</param>
    /// <returns>The view.</returns>
    /// <exception cref="ArgumentNullException">The grant is absent.</exception>
    public static OwnGrantView Of(OwnGrant grant)
    {
        ArgumentNullException.ThrowIfNull(grant);

        return new OwnGrantView(
            grant.Id.Value,
            grant.Kind,
            grant.On.Type.ToString(),
            grant.On.Id.ToString(),
            grant.Role.ToString(),
            grant.Deny,
            grant.ExpiresAt,
            grant.GrantedAt);
    }
}
