using System;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Invitations;
using Janus.Core;

namespace Janus.Authentication.Organizations;

/// <summary>
/// Where a change to an organization's lifecycle, to its domain lock, to the
/// invitations into it or to its memberships is written down: who, which organization,
/// what, when and why.
/// </summary>
/// <remarks>
/// Implements IDN-ORG-003, REG-DOM-001, IDN-LIFE-009a, IDN-MEM-001, IDN-AUD-001 and
/// IDN-PRIN-001.
/// </remarks>
internal interface IOrganizationAudit
{
    /// <summary>
    /// Records a change to an organization's lifecycle.
    /// </summary>
    /// <param name="action">What changed.</param>
    /// <param name="organization">Which organization.</param>
    /// <param name="reason">Why.</param>
    /// <param name="actor">Who made the change.</param>
    /// <param name="breakGlassReason">
    /// The reason given at the use of the break-glass credential, where the action was
    /// taken in the session it opened, or nothing.
    /// </param>
    /// <param name="at">When.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of recording it.</returns>
    ValueTask RecordedAsync(
        AuditAction action,
        OrganizationId organization,
        string reason,
        SubjectId actor,
        string? breakGlassReason,
        DateTimeOffset at,
        CancellationToken cancellationToken);

    /// <summary>
    /// Records that a system principal did something to an organization, as bootstrap
    /// creates the administrative one (IDN-PRIN-001).
    /// </summary>
    /// <param name="action">What happened.</param>
    /// <param name="organization">Which organization.</param>
    /// <param name="principal">The principal that did it, with its stated reason.</param>
    /// <param name="at">When.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of recording it.</returns>
    ValueTask RecordedAsync(
        AuditAction action,
        OrganizationId organization,
        SystemPrincipal principal,
        DateTimeOffset at,
        CancellationToken cancellationToken);

    /// <summary>
    /// Records that a command on the server did something to an organization under its
    /// principal, with the reason the person running it gave (IDN-PRIN-001, D-248).
    /// </summary>
    /// <param name="action">What happened.</param>
    /// <param name="organization">Which organization.</param>
    /// <param name="reason">Why, as the command was told.</param>
    /// <param name="principal">The principal the command runs as, with its stated reason.</param>
    /// <param name="at">When.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of recording it.</returns>
    ValueTask RecordedAsync(
        AuditAction action,
        OrganizationId organization,
        string reason,
        SystemPrincipal principal,
        DateTimeOffset at,
        CancellationToken cancellationToken);

    /// <summary>
    /// Records a change to one domain of an organization's lock.
    /// </summary>
    /// <param name="action">What changed.</param>
    /// <param name="organization">Which organization.</param>
    /// <param name="domain">Which domain.</param>
    /// <param name="reason">Why.</param>
    /// <param name="actor">Who made the change.</param>
    /// <param name="breakGlassReason">
    /// The reason given at the use of the break-glass credential, where the action was
    /// taken in the session it opened, or nothing.
    /// </param>
    /// <param name="at">When.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of recording it.</returns>
    ValueTask DomainChangedAsync(
        AuditAction action,
        OrganizationId organization,
        string domain,
        string reason,
        SubjectId actor,
        string? breakGlassReason,
        DateTimeOffset at,
        CancellationToken cancellationToken);

    /// <summary>
    /// Records an invitation issued into an organization, or one revoked. What the
    /// invitation binds is someone's personal data before any account of theirs exists,
    /// so the record names the invitation and nothing it binds, and, where it takes over
    /// a mailbox someone has held, what becomes of that mailbox and why.
    /// </summary>
    /// <param name="action">What changed.</param>
    /// <param name="organization">Which organization.</param>
    /// <param name="invitation">Which invitation.</param>
    /// <param name="takeover">What the invitation chose for a former mailbox, or nothing.</param>
    /// <param name="actor">Who made the change.</param>
    /// <param name="breakGlassReason">
    /// The reason given at the use of the break-glass credential, where the action was
    /// taken in the session it opened, or nothing.
    /// </param>
    /// <param name="at">When.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of recording it.</returns>
    ValueTask InvitationChangedAsync(
        AuditAction action,
        OrganizationId organization,
        InvitationId invitation,
        MailboxTakeover? takeover,
        SubjectId actor,
        string? breakGlassReason,
        DateTimeOffset at,
        CancellationToken cancellationToken);

    /// <summary>
    /// Records a mailbox given to a current member of the administrative organization,
    /// filed under it with the member as whom it was done to, and never the address
    /// (REG-MAIL-001, D-253).
    /// </summary>
    /// <param name="organization">The administrative organization.</param>
    /// <param name="mailbox">Which mailbox.</param>
    /// <param name="member">Whose it became.</param>
    /// <param name="reason">Why, as given.</param>
    /// <param name="formerMailbox">What the request chose for a mailbox the address had, or nothing.</param>
    /// <param name="actor">Who gave it.</param>
    /// <param name="breakGlassReason">The break-glass reason the change was made under, or nothing.</param>
    /// <param name="at">When.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of recording it.</returns>
    ValueTask MailboxGivenAsync(
        OrganizationId organization,
        MailboxId mailbox,
        SubjectId member,
        string reason,
        FormerMailbox? formerMailbox,
        SubjectId actor,
        string? breakGlassReason,
        DateTimeOffset at,
        CancellationToken cancellationToken);

    /// <summary>
    /// Records a mailbox reserved under <c>formerMailbox</c> <c>adopt</c>, whose push takes
    /// over the account the mail server holds, filed under the administrative
    /// organization and never with the address (REG-MAIL-003, D-255).
    /// </summary>
    /// <param name="organization">The administrative organization.</param>
    /// <param name="mailbox">Which mailbox.</param>
    /// <param name="subject">Whom it was done to: the member given it, or the administrator inviting.</param>
    /// <param name="reason">Why, as given.</param>
    /// <param name="actor">Who named it.</param>
    /// <param name="breakGlassReason">The break-glass reason the change was made under, or nothing.</param>
    /// <param name="at">When.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of recording it.</returns>
    ValueTask MailboxAdoptedAsync(
        OrganizationId organization,
        MailboxId mailbox,
        SubjectId subject,
        string reason,
        SubjectId actor,
        string? breakGlassReason,
        DateTimeOffset at,
        CancellationToken cancellationToken);

    /// <summary>
    /// Records a membership an administrator ended, filed under the organization with the
    /// member as whom it was done to.
    /// </summary>
    /// <param name="organization">Which organization.</param>
    /// <param name="membership">Which membership.</param>
    /// <param name="member">Whose membership it was.</param>
    /// <param name="reason">Why.</param>
    /// <param name="actor">Who ended it.</param>
    /// <param name="breakGlassReason">
    /// The reason given at the use of the break-glass credential, where the action was
    /// taken in the session it opened, or nothing.
    /// </param>
    /// <param name="at">When.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of recording it.</returns>
    ValueTask MembershipEndedAsync(
        OrganizationId organization,
        MembershipId membership,
        SubjectId member,
        string reason,
        SubjectId actor,
        string? breakGlassReason,
        DateTimeOffset at,
        CancellationToken cancellationToken);

    /// <summary>
    /// Records a membership its member ended by bringing forward the end an administrator
    /// had set, the member acting, with the reason <c>broughtforward</c> and the end it
    /// replaced, filed under the organization (IDN-MEM-001, D-240).
    /// </summary>
    /// <param name="organization">Which organization.</param>
    /// <param name="membership">Which membership.</param>
    /// <param name="member">Whose membership it was, who ended it.</param>
    /// <param name="replaced">The instant the end had been set to.</param>
    /// <param name="breakGlassReason">The break-glass reason the session carries, or nothing.</param>
    /// <param name="at">When.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of recording it.</returns>
    ValueTask MembershipEndBroughtForwardAsync(
        OrganizationId organization,
        MembershipId membership,
        SubjectId member,
        DateTimeOffset replaced,
        string? breakGlassReason,
        DateTimeOffset at,
        CancellationToken cancellationToken);

    /// <summary>
    /// Records a membership the job <c>membership-ends</c> ended at the instant an
    /// administrator set, under its principal, naming that administrator, filed under the
    /// organization with the member as whom it was done to (IDN-MEM-001, D-217).
    /// </summary>
    /// <param name="organization">Which organization.</param>
    /// <param name="membership">Which membership.</param>
    /// <param name="member">Whose membership it was.</param>
    /// <param name="reason">The reason the end was set with.</param>
    /// <param name="principal">The principal that ended it.</param>
    /// <param name="endSetBy">The administrator who set the end.</param>
    /// <param name="at">When.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of recording it.</returns>
    ValueTask MembershipEndedAsync(
        OrganizationId organization,
        MembershipId membership,
        SubjectId member,
        string reason,
        SystemPrincipal principal,
        SubjectId endSetBy,
        DateTimeOffset at,
        CancellationToken cancellationToken);

    /// <summary>
    /// Records an end an administrator set a membership to reach at a later instant,
    /// moved or cancelled, filed under the organization with the member as whom it was
    /// done to.
    /// </summary>
    /// <param name="organization">Which organization.</param>
    /// <param name="membership">Which membership.</param>
    /// <param name="member">Whose membership it is.</param>
    /// <param name="endsAt">The instant set, or nothing where the end was cancelled.</param>
    /// <param name="reason">Why.</param>
    /// <param name="actor">Who set, moved or cancelled it.</param>
    /// <param name="breakGlassReason">
    /// The reason given at the use of the break-glass credential, where the action was
    /// taken in the session it opened, or nothing.
    /// </param>
    /// <param name="at">When.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of recording it.</returns>
    ValueTask MembershipEndSetAsync(
        OrganizationId organization,
        MembershipId membership,
        SubjectId member,
        DateTimeOffset? endsAt,
        string reason,
        SubjectId actor,
        string? breakGlassReason,
        DateTimeOffset at,
        CancellationToken cancellationToken);
}
