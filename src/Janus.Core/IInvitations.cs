using System;
using System.Threading;
using System.Threading.Tasks;

namespace Janus.Core;

/// <summary>
/// The invitations an organization issues into its membership, and the end of a
/// membership, under <c>membership:manage</c> in that organization.
/// </summary>
/// <remarks>
/// Implements LIB-API-005, IDN-LIFE-009a, IDN-MEM-001, REG-INV-001, REG-MAIL-001,
/// REG-MAIL-003 and chapter 09 section 8a. An invitation is a time-boxed, single-use
/// link; issuing one is the <c>invitation:issue</c> step-up action, and roles it
/// attaches also need <c>grant:manage</c> in the organization.
/// </remarks>
public interface IInvitations
{
    /// <summary>
    /// Issues an invitation, reserving the corporate mailbox where the organization's
    /// mail is integrated and sending the link where an email is bound.
    /// </summary>
    /// <param name="context">Who is inviting.</param>
    /// <param name="session">The session the step-up is judged on.</param>
    /// <param name="organization">Into which organization.</param>
    /// <param name="request">What the invitation names.</param>
    /// <param name="source">The address the request came from, which the send counts against.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// The invitation, or the refusal: <c>identity.organization.notfound</c> where the
    /// deployment holds no such organization, <c>authz.denied</c> where its deletion was
    /// requested, <c>identity.identifier.invalid</c> naming the member where an
    /// identifier is malformed, <c>identity.invitation.addressrequired</c> naming the
    /// member where one the organization's mail requires is absent or both name one
    /// address, <c>identity.identifier.mixedscript</c> naming the member where a word
    /// mixes scripts, <c>identity.identifier.domainnotallowed</c> where the
    /// organization's lock does not admit the address the member will sign in with,
    /// <c>identity.mailbox.taken</c> naming <c>corporateEmail</c> where a member holds the
    /// corporate address or a standing invitation reserves it,
    /// <c>api.request.malformed</c> naming the member that cannot be taken.
    /// </returns>
    ValueTask<Result<IssuedInvitation>> IssueAsync(
        AccessContext context,
        SessionId session,
        OrganizationId organization,
        InvitationRequest request,
        string source,
        CancellationToken cancellationToken);

    /// <summary>
    /// Revokes an invitation nobody has acknowledged, giving up the mailbox it reserved
    /// where nobody ever held it.
    /// </summary>
    /// <param name="context">Who is revoking.</param>
    /// <param name="organization">Whose invitation.</param>
    /// <param name="invitation">Which invitation.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// Success, which a revoked invitation answers again, or the refusal:
    /// <c>identity.organization.notfound</c> where the deployment holds no such
    /// organization, <c>identity.invitation.notfound</c> where the organization issued no
    /// such invitation, <c>identity.invitation.expired</c> where it has been acknowledged
    /// and is used.
    /// </returns>
    ValueTask<Result> RevokeAsync(
        AccessContext context,
        OrganizationId organization,
        InvitationId invitation,
        CancellationToken cancellationToken);

    /// <summary>
    /// Reads the invitation attached to the signed-in person's account, which the
    /// membership step shows before it is acknowledged (REG-INV-002): the one whose
    /// link the account opened most recently, among those neither acknowledged nor
    /// revoked, expired or not.
    /// </summary>
    /// <param name="context">Who is signed in.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// The invitation, or the refusal: <c>identity.invitation.notfound</c> where none is
    /// attached.
    /// </returns>
    ValueTask<Result<AttachedInvitation>> AttachedAsync(
        AccessContext context,
        CancellationToken cancellationToken);

    /// <summary>
    /// Acknowledges the invitation the membership step showed: the membership attaches
    /// carrying the documents at their versions, the roles are granted, and where the
    /// organization's mail is integrated the corporate address becomes the primary
    /// email beside the personal one (REG-INV-001, REG-INV-002, REG-MAIL-001).
    /// </summary>
    /// <param name="context">Who is signed in.</param>
    /// <param name="invitation">The invitation the membership step showed.</param>
    /// <param name="source">The address the request came from, which a notice counts against.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// Success, or the refusal: <c>identity.invitation.notfound</c> where no such
    /// invitation is attached to the account; <c>identity.invitation.expired</c> where it
    /// no longer stands; <c>identity.invitation.identifiermismatch</c> where an
    /// identifier it binds is not verified on the account;
    /// <c>identity.identifier.domainnotallowed</c> where the organization's lock refuses
    /// the address the member will sign in with; <c>identity.membership.limitreached</c>
    /// or <c>identity.identifier.maximum</c> where the account can take no more, told
    /// before the credential policy; <c>auth.stepup.required</c> with outcome
    /// <c>enrol</c> and the <c>policyRequirement</c> unmet where the account does not
    /// meet the organization's credential policy.
    /// </returns>
    ValueTask<Result> AcknowledgeAsync(
        AccessContext context,
        InvitationId invitation,
        string source,
        CancellationToken cancellationToken);

    /// <summary>
    /// Ends an account's membership of the organization; the account and its state
    /// persist. In the same operation the grants a person gave the account in the
    /// organization are revoked and the account leaves the organization's groups, and for
    /// the administrative organization the same in every organization the account holds
    /// no current membership of (IDN-MEM-001); where the membership gave the account a
    /// corporate address, the address and its mailbox are retired and the personal email
    /// becomes the primary (REG-MAIL-003).
    /// </summary>
    /// <param name="context">Who is ending it.</param>
    /// <param name="session">The session the step-up is judged on.</param>
    /// <param name="organization">Of which organization.</param>
    /// <param name="member">Whose membership.</param>
    /// <param name="reason">Why, which each record the end writes carries.</param>
    /// <param name="source">The address the request came from, which a notice counts against.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// Success, or the refusal: <c>identity.organization.notfound</c> where the
    /// deployment holds no such organization, <c>authz.denied</c> where no person asks
    /// or the person may not manage its memberships, <c>api.request.malformed</c> naming
    /// <c>reason</c> where it is blank or longer than 1024 characters after trimming,
    /// <c>authz.role.notassignable</c> where the person may not hand out a role the end
    /// reaches, <c>identity.membership.notfound</c> where the account holds no current
    /// membership of the organization, <c>auth.stepup.required</c> where the session
    /// does not meet the <c>membership:end</c> gate.
    /// </returns>
    ValueTask<Result> EndMembershipAsync(
        AccessContext context,
        SessionId session,
        OrganizationId organization,
        SubjectId member,
        string reason,
        string source,
        CancellationToken cancellationToken);

    /// <summary>
    /// Sets an account's membership of the organization to end at a later instant, or
    /// moves the instant set (IDN-MEM-001, D-213). The end is judged as an end now is,
    /// when it is set.
    /// </summary>
    /// <param name="context">Who is setting it.</param>
    /// <param name="session">The session the step-up is judged on.</param>
    /// <param name="organization">Of which organization.</param>
    /// <param name="member">Whose membership.</param>
    /// <param name="endsAt">The instant it ends, after the present.</param>
    /// <param name="reason">Why, which the end is recorded with.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// Success, or the refusal of <see cref="EndMembershipAsync"/> but its
    /// <c>identity.membership.notfound</c> for a second end, or
    /// <c>api.request.invalid</c> naming <c>endsAt</c> where it is not after the present.
    /// </returns>
    ValueTask<Result> SetMembershipEndAsync(
        AccessContext context,
        SessionId session,
        OrganizationId organization,
        SubjectId member,
        DateTimeOffset endsAt,
        string reason,
        CancellationToken cancellationToken);

    /// <summary>
    /// Cancels the end an account's membership of the organization is set to reach
    /// (IDN-MEM-001, D-213).
    /// </summary>
    /// <param name="context">Who is cancelling it.</param>
    /// <param name="session">The session the step-up is judged on.</param>
    /// <param name="organization">Of which organization.</param>
    /// <param name="member">Whose membership.</param>
    /// <param name="reason">Why, which the cancellation is recorded with.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// Success, or the refusal of <see cref="EndMembershipAsync"/>, with
    /// <c>identity.membership.notfound</c> too where no end is set.
    /// </returns>
    ValueTask<Result> CancelMembershipEndAsync(
        AccessContext context,
        SessionId session,
        OrganizationId organization,
        SubjectId member,
        string reason,
        CancellationToken cancellationToken);
}
