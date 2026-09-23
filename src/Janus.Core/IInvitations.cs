using System.Threading;
using System.Threading.Tasks;

namespace Janus.Core;

/// <summary>
/// The invitations an organization issues into its membership, under
/// <c>membership:manage</c> in that organization.
/// </summary>
/// <remarks>
/// Implements LIB-API-005, IDN-LIFE-009a, REG-INV-001, REG-MAIL-001 and chapter 09
/// section 8a. An invitation is a time-boxed, single-use link; issuing one is the
/// <c>invitation:issue</c> step-up action, and roles it attaches also need
/// <c>grant:manage</c> in the organization.
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
    /// The invitation, or the refusal: <c>identity.identifier.invalid</c> naming the
    /// member where an identifier is malformed or one the organization's mail requires
    /// is absent, <c>identity.identifier.mixedscript</c> naming the member where a word
    /// mixes scripts, <c>identity.identifier.domainnotallowed</c> where the
    /// organization's lock does not admit the address the member will sign in with,
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
    /// <c>api.request.malformed</c> naming <c>invitationId</c> where the organization
    /// issued no such invitation, <c>identity.invitation.expired</c> where it has been
    /// acknowledged and is used.
    /// </returns>
    ValueTask<Result> RevokeAsync(
        AccessContext context,
        OrganizationId organization,
        InvitationId invitation,
        CancellationToken cancellationToken);
}
