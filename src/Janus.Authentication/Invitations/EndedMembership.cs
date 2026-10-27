using Janus.Core;

namespace Janus.Authentication.Invitations;

/// <summary>
/// A membership an end closed.
/// </summary>
/// <param name="Membership">The membership.</param>
/// <param name="Invitation">
/// The invitation whose acknowledgement began it, or nothing where it began otherwise.
/// </param>
/// <remarks>
/// Implements IDN-MEM-001 and IDN-READ-002 (D-218): what the end announces.
/// </remarks>
internal sealed record EndedMembership(MembershipId Membership, InvitationId? Invitation);
