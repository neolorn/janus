using Janus.Core;

namespace Janus.Privacy.Erasures;

/// <summary>
/// A membership an organization's erasure ended: which it was, whose, and the invitation
/// it began from.
/// </summary>
/// <param name="Membership">Which membership.</param>
/// <param name="Subject">Whose it was.</param>
/// <param name="Invitation">
/// The invitation whose acknowledgement began it, or nothing where it began otherwise.
/// </param>
/// <remarks>Implements IDN-ORG-003, IDN-MEM-001 and IDN-READ-002 (D-218).</remarks>
internal sealed record EndedMembership(MembershipId Membership, SubjectId Subject, InvitationId? Invitation);
