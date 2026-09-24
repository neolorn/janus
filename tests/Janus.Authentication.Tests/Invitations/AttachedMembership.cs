using System;
using System.Collections.Generic;
using Janus.Core;

namespace Janus.Authentication.Tests.Invitations;

/// <summary>
/// One membership an acknowledgement attached, and the grants it carried.
/// </summary>
/// <param name="Id">The membership.</param>
/// <param name="Subject">Whose.</param>
/// <param name="Organization">Of which organization.</param>
/// <param name="Acknowledged">The documents at the versions acknowledged.</param>
/// <param name="Roles">The roles granted across the organization, each once.</param>
/// <param name="GrantedBy">Who granted them.</param>
/// <param name="Reason">Why.</param>
/// <param name="At">When.</param>
internal sealed record AttachedMembership(
    MembershipId Id,
    SubjectId Subject,
    OrganizationId Organization,
    IReadOnlyList<InvitationDocument> Acknowledged,
    IReadOnlyList<RoleName> Roles,
    SubjectId GrantedBy,
    string Reason,
    DateTimeOffset At);
