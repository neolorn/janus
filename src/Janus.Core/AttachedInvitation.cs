using System;
using System.Collections.Generic;

namespace Janus.Core;

/// <summary>
/// The invitation attached to a person's account, as the membership step shows it
/// before they acknowledge it.
/// </summary>
/// <param name="Id">The invitation.</param>
/// <param name="Organization">Into which organization.</param>
/// <param name="OrganizationName">What the organization is called.</param>
/// <param name="InvitedBy">
/// The display name of who issued it, or nothing where that account shows none.
/// </param>
/// <param name="Roles">The roles granted across the organization when the membership attaches.</param>
/// <param name="Documents">The documents the person acknowledges, at the versions they are shown.</param>
/// <param name="ExpiresAt">When it can no longer be acknowledged.</param>
/// <remarks>Implements REG-INV-001, REG-INV-002 and chapter 09 section 6a.</remarks>
public sealed record AttachedInvitation(
    InvitationId Id,
    OrganizationId Organization,
    string OrganizationName,
    string? InvitedBy,
    IReadOnlyList<RoleName> Roles,
    IReadOnlyList<InvitationDocument> Documents,
    DateTimeOffset ExpiresAt);
