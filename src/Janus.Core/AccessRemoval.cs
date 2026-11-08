using System.Collections.Generic;

namespace Janus.Core;

/// <summary>
/// One removal of access the host's check is asked about: a grant revoked, a membership
/// ended or a member taken out of a group.
/// </summary>
/// <param name="Kind">Which removal it is.</param>
/// <param name="Organization">The organization the change is made in.</param>
/// <param name="Subject">Whose access is removed: the grant's holder, the member, or the account whose membership ends.</param>
/// <param name="Grant">The grant revoked, where one is; nothing for the other kinds.</param>
/// <param name="Role">The role of the grant revoked, where one is; nothing for the other kinds.</param>
/// <param name="Group">The group a member is taken out of, where one is; nothing for the other kinds.</param>
/// <param name="Context">Who is making the change: a person, or the job's principal at a scheduled end.</param>
/// <param name="Reason">Why, as the change records it; at a scheduled end, the reason given when the end was set.</param>
/// <param name="Path">Which way the change came.</param>
/// <param name="Grants">
/// The allow grants the change revokes or takes away: for a membership's end, those of the
/// account the end revokes; for a group member's removal, those the group reaches; empty
/// for a grant's revocation, which names its grant.
/// </param>
/// <param name="Groups">The groups a membership's end takes the account out of; empty for the other kinds.</param>
/// <remarks>Implements AUTHZ-GRANT-005 (D-281).</remarks>
public sealed record AccessRemoval(
    AccessRemovalKind Kind,
    OrganizationId Organization,
    GrantSubject Subject,
    GrantId? Grant,
    RoleName? Role,
    GroupId? Group,
    AccessContext Context,
    string Reason,
    AccessRemovalPath Path,
    IReadOnlyList<RemovedGrant> Grants,
    IReadOnlyList<GroupId> Groups);
