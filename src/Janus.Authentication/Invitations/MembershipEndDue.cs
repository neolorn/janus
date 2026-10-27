using Janus.Core;

namespace Janus.Authentication.Invitations;

/// <summary>
/// A membership whose end, set in advance, the clock has reached.
/// </summary>
/// <param name="Membership">The membership.</param>
/// <param name="Member">Whose membership.</param>
/// <param name="Organization">Of which organization.</param>
/// <param name="SetBy">The administrator who set the end.</param>
/// <param name="Reason">The reason they gave.</param>
/// <param name="Invitation">
/// The invitation whose acknowledgement began the membership, or nothing where it began
/// otherwise.
/// </param>
/// <remarks>
/// Implements IDN-MEM-001 and IDN-READ-002 (D-213, D-217, D-218): what the job
/// <c>membership-ends</c> carries out, and what each record it writes names.
/// </remarks>
internal sealed record MembershipEndDue(
    MembershipId Membership,
    SubjectId Member,
    OrganizationId Organization,
    SubjectId SetBy,
    string Reason,
    InvitationId? Invitation);
