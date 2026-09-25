using System;
using System.Collections.Generic;
using System.Linq;
using Janus.Core;

namespace Janus.Hosting.Accounts;

/// <summary>
/// The invitation attached to the account, as the membership step reads it.
/// </summary>
/// <param name="Id">The invitation.</param>
/// <param name="Organization">Into which organization.</param>
/// <param name="OrganizationName">What the organization is called.</param>
/// <param name="InvitedBy">The display name of who issued it, or nothing where that account shows none.</param>
/// <param name="Roles">The roles granted across the organization when the membership attaches.</param>
/// <param name="Documents">The documents the person acknowledges, at the versions they are shown.</param>
/// <param name="ExpiresAt">When it can no longer be acknowledged.</param>
/// <remarks>Implements chapter 09 section 6a, REG-INV-002 and API-CONV-002.</remarks>
internal sealed record AttachedInvitationView(
    Guid Id,
    Guid Organization,
    string OrganizationName,
    string? InvitedBy,
    IReadOnlyList<string> Roles,
    IReadOnlyList<InvitationDocument> Documents,
    DateTimeOffset ExpiresAt)
{
    /// <summary>
    /// The view of one attached invitation.
    /// </summary>
    /// <param name="attached">The invitation.</param>
    /// <returns>The view.</returns>
    /// <exception cref="ArgumentNullException">The invitation is absent.</exception>
    public static AttachedInvitationView Of(AttachedInvitation attached)
    {
        ArgumentNullException.ThrowIfNull(attached);

        return new(
            attached.Id.Value,
            attached.Organization.Value,
            attached.OrganizationName,
            attached.InvitedBy,
            [.. attached.Roles.Select(role => role.ToString())],
            attached.Documents,
            attached.ExpiresAt);
    }
}
