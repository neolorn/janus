using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Accounts;
using Janus.Authentication.Factors;
using Janus.Authentication.Mailboxes;
using Janus.Authentication.Organizations;
using Janus.Authentication.Policies;
using Janus.Core;
using Janus.Core.Configuration;

namespace Janus.Authentication.Invitations;

/// <summary>
/// The invitations an organization issues into its membership, their revocation, and
/// the end of a membership.
/// </summary>
/// <param name="gate">The one place a permission is evaluated.</param>
/// <param name="scope">Whether the caller may attach a role that administers the deployment.</param>
/// <param name="stepUp">What issuing asks of the caller's session.</param>
/// <param name="directory">Where the organization is found.</param>
/// <param name="roles">Where the roles an invitation names are looked up.</param>
/// <param name="legal">Where the documents an invitation names are resolved to a version.</param>
/// <param name="locks">Whether the organization's domain lock admits an address.</param>
/// <param name="invitations">Where invitations are kept.</param>
/// <param name="accounts">Where the name of who issued an invitation is read.</param>
/// <param name="acknowledgement">What attaches the membership an invitation offers.</param>
/// <param name="end">What ends a membership.</param>
/// <param name="mailboxes">Where the corporate mailboxes are reserved.</param>
/// <param name="inUse">
/// The mail server in use, which the administrative organization's mail is integrated
/// with where the deployment has one.
/// </param>
/// <param name="sending">What carries the link.</param>
/// <param name="configuration">Where the lifetime and the languages are read.</param>
/// <param name="audit">Where every issue and revocation is written down.</param>
/// <param name="work">The one transaction an operation runs in.</param>
/// <param name="time">The clock the deployment runs on.</param>
/// <param name="randomness">Where the link's token is drawn from.</param>
/// <remarks>
/// Implements LIB-API-005, IDN-LIFE-009a, REG-INV-001, REG-MAIL-001, REG-MAIL-003,
/// INT-MAIL-006 and chapter 09 section 8a. The mailboxes are the administrative
/// organization's, so its mail is integrated exactly where it is the organization
/// invited into and the deployment registered a mail server. A mailbox someone has held
/// passes to nobody without the administrator's choice: the invitation names it, with
/// a reason, and the issue's step-up and audit record carry it (D-166, D-178). The link goes out before anything is written,
/// so an invitation whose link could not be sent is never issued; one whose writing
/// fails leaves a link that opens nothing.
/// </remarks>
internal sealed class InvitationService(
    IAccessGate gate,
    AdministrativeScope scope,
    StepUpGuard stepUp,
    IOrganizationDirectory directory,
    IRoleCatalogue roles,
    ILegalDocuments legal,
    DomainLock locks,
    IInvitationStore invitations,
    IAccountDirectory accounts,
    InvitationAcknowledgement acknowledgement,
    MembershipEnd end,
    IMailboxStore mailboxes,
    IMailServerInUse inUse,
    INotificationHandler sending,
    IConfigurationStore configuration,
    IOrganizationAudit audit,
    IUnitOfWork work,
    TimeProvider time,
    RandomNumberGenerator randomness) : IInvitations
{
    /// <inheritdoc/>
    public async ValueTask<Result<IssuedInvitation>> IssueAsync(
        AccessContext context,
        SessionId session,
        OrganizationId organization,
        InvitationRequest request,
        string source,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(source);

        // An invitation is issued by a person, whose identity the record carries.
        if (context.Acting is not SubjectId acting)
        {
            return Result.Failure<IssuedInvitation>(Error.From(ErrorCodes.Denied));
        }

        // 09 section 8: a path naming no organization is answered for the organization,
        // whichever organization the permission is asked in.
        if (await directory.FindAsync(organization, cancellationToken).ConfigureAwait(false)
            is not OrganizationStanding standing)
        {
            return Result.Failure<IssuedInvitation>(Error.From(ErrorCodes.OrganizationNotFound));
        }

        if (await RefusedAsync(context, Permissions.MembershipManage, organization, cancellationToken)
                .ConfigureAwait(false)
            is Error refused)
        {
            return Result.Failure<IssuedInvitation>(refused);
        }

        // IDN-ORG-003 AC12: an organization on its way out takes no new members, since
        // its grants confer nothing, so the issue is refused as the gate refuses.
        if (standing.DeletionRequestedAt is not null)
        {
            return Result.Failure<IssuedInvitation>(Error.From(ErrorCodes.Denied));
        }

        bool integrated = standing.IsAdministrative && inUse.Chosen().Match(_ => true, _ => false);
        Error? failure = null;

        Bound bound = (await BoundAsync(organization, request, integrated, cancellationToken)
                .ConfigureAwait(false))
            .Match(value => value, error => Withheld<Bound>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure<IssuedInvitation>(failure);
        }

        MailboxTakeover? takeover = Takeover(request)
            .Match(value => value, error => Withheld<MailboxTakeover?>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure<IssuedInvitation>(failure);
        }

        IReadOnlyList<RoleName> attached = (await RolesAsync(context, organization, request.Roles, cancellationToken)
                .ConfigureAwait(false))
            .Match(value => value, error => Withheld<IReadOnlyList<RoleName>>(error, ref failure));

        IReadOnlyList<InvitationDocument> shown = failure is null
            ? (await DocumentsAsync(request.Documents, cancellationToken).ConfigureAwait(false))
                .Match(value => value, error => Withheld<IReadOnlyList<InvitationDocument>>(error, ref failure))
            : [];

        TimeSpan lifetime = (await configuration
                .ReadAsync(Settings.LinkInvitationLifetime, cancellationToken).ConfigureAwait(false))
            .Match(value => value, error => Withheld<TimeSpan>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure<IssuedInvitation>(failure);
        }

        DateTimeOffset now = time.GetUtcNow();

        Reservation? reservation = null;

        if (bound.Corporate is EmailAddress corporate)
        {
            reservation = (await ReservedAsync(corporate, takeover, now, cancellationToken).ConfigureAwait(false))
                .Match(value => value, error => Withheld<Reservation>(error, ref failure));

            if (failure is not null)
            {
                return Result.Failure<IssuedInvitation>(failure);
            }
        }
        else if (takeover is not null)
        {
            // REG-MAIL-003, D-178: with no corporate address no held mailbox stands for
            // one, so there is nothing for the choice to act on.
            return Result.Failure<IssuedInvitation>(Named(ErrorCodes.RequestInvalid, "formerMailbox"));
        }

        if (await stepUp
                .PassedAsync(acting, session, StepUpAction.InvitationIssue, cancellationToken)
                .ConfigureAwait(false)
            is Error challenged)
        {
            return Result.Failure<IssuedInvitation>(challenged);
        }

        // REG-INV-001: a role named is granted when the membership attaches, so the
        // issue is also the step-up a grant is, judged after the issue's own.
        if (attached.Count > 0
            && await stepUp
                .PassedAsync(acting, session, StepUpAction.GrantManage, cancellationToken)
                .ConfigureAwait(false)
            is Error ungranted)
        {
            return Result.Failure<IssuedInvitation>(ungranted);
        }

        var token = OpaqueToken.Draw(randomness);
        var invitation = Invitation.Issued(
            InvitationId.New(time),
            organization,
            acting,
            bound.Identifiers,
            attached,
            shown,
            reservation?.Mailbox.Id,
            token.Fingerprint(),
            now,
            lifetime);

        if (bound.Linked is EmailAddress linked
            && await SentAsync(linked, token, source, cancellationToken).ConfigureAwait(false) is Error unsent)
        {
            return Result.Failure<IssuedInvitation>(unsent);
        }

        if ((await work.BeginAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(() => null, error => error) is Error notBegun)
        {
            return Result.Failure<IssuedInvitation>(notBegun);
        }

        if (reservation is not null)
        {
            await ReserveAsync(reservation, acting, context.BreakGlassReason, now, cancellationToken).ConfigureAwait(false);
        }

        await invitations.AddAsync(invitation, cancellationToken).ConfigureAwait(false);
        await audit
            .InvitationChangedAsync(
                AuditActions.InvitationIssued,
                organization,
                invitation.Id,
                takeover,
                acting,
                context.BreakGlassReason,
                now,
                cancellationToken)
            .ConfigureAwait(false);

        if ((await work.CommitAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(() => null, error => error) is Error notCommitted)
        {
            return Result.Failure<IssuedInvitation>(notCommitted);
        }

        // The token is answered only where no email is bound for the link to go to:
        // the administrator hands it over, and nothing else ever shows it again.
        return Result.Success(new IssuedInvitation(
            invitation.Id,
            invitation.ExpiresAt,
            bound.Linked is null ? token.Value : null));
    }

    /// <inheritdoc/>
    public async ValueTask<Result> RevokeAsync(
        AccessContext context,
        OrganizationId organization,
        InvitationId invitation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Acting is not SubjectId acting)
        {
            return Result.Failure(Error.From(ErrorCodes.Denied));
        }

        // 09 section 8: a path naming no organization is answered for the organization,
        // whichever organization the permission is asked in.
        if (await directory.FindAsync(organization, cancellationToken).ConfigureAwait(false) is null)
        {
            return Result.Failure(Error.From(ErrorCodes.OrganizationNotFound));
        }

        if (await RefusedAsync(context, Permissions.MembershipManage, organization, cancellationToken)
                .ConfigureAwait(false)
            is Error refused)
        {
            return Result.Failure(refused);
        }

        if (await invitations.FindAsync(invitation, cancellationToken).ConfigureAwait(false)
                is not Invitation held
            || held.Organization != organization)
        {
            return Result.Failure(Malformed("invitationId"));
        }

        if (held.IsAcknowledged)
        {
            return Result.Failure(Error.From(ErrorCodes.InvitationExpired));
        }

        if (held.IsRevoked)
        {
            return Result.Success();
        }

        DateTimeOffset now = time.GetUtcNow();

        if ((await work.BeginAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(() => null, error => error) is Error notBegun)
        {
            return Result.Failure(notBegun);
        }

        await WithdrawnAsync(held, acting, context.BreakGlassReason, now, cancellationToken).ConfigureAwait(false);

        // REG-MAIL-001: a reservation nobody ever took is given up with the invitation
        // that made it; a mailbox someone has held stays, disabled.
        if (held.Mailbox is MailboxId reserved
            && await mailboxes.FindAsync(reserved, cancellationToken).ConfigureAwait(false)
                is { IsRemovable: true } mailbox)
        {
            mailbox.Release(now);

            await mailboxes.RecordAsync(mailbox, cancellationToken).ConfigureAwait(false);
        }

        if ((await work.CommitAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(() => null, error => error) is Error notCommitted)
        {
            return Result.Failure(notCommitted);
        }

        return Result.Success();
    }

    private static Error Malformed(string member) =>
        Error.From(ErrorCodes.RequestMalformed, "member", JsonSerializer.SerializeToElement(member));

    private static Error Named(ErrorCode code, string member) =>
        Error.From(code, "member", JsonSerializer.SerializeToElement(member));

    private static TValue Withheld<TValue>(Error error, ref Error? failure)
    {
        failure = error;

        return default!;
    }

    // What the administrator entered, as it is pre-filled at registration, beside the
    // canonical form every rule is judged on.
    private static Result<Entered?> Read(IdentifierKind kind, string? value, string member)
    {
        if (value is null)
        {
            return Result.Success<Entered?>(null);
        }

        string entered = value.Trim();

        Entered? read = kind is IdentifierKind.Email
            ? EmailAddress.TryParse(entered, out EmailAddress address)
                ? new Entered(entered, address.Value, address)
                : null
            : PhoneNumber.TryParse(entered, out PhoneNumber number)
                ? new Entered(entered, number.Value, Address: null)
                : null;

        if (read is null)
        {
            return Result.Failure<Entered?>(Named(ErrorCodes.IdentifierInvalid, member));
        }

        if (!ScriptMixing.IsSingleScriptPerWord(read.Canonical))
        {
            return Result.Failure<Entered?>(Named(ErrorCodes.IdentifierMixedScript, member));
        }

        return Result.Success<Entered?>(read);
    }

    // REG-INV-001 and REG-MAIL-001: which identifiers the invitation binds, where the
    // link goes, and which address the organization's lock is asked about: the
    // corporate address where the mail is integrated, the bound email otherwise. The
    // personal email of an integrated invitation is the one the lock keeps out of
    // sign-in during the membership, so it is not asked about.
    private async ValueTask<Result<Bound>> BoundAsync(
        OrganizationId organization,
        InvitationRequest request,
        bool integrated,
        CancellationToken cancellationToken)
    {
        Error? failure = null;

        Entered? email = Read(IdentifierKind.Email, request.Email, "email")
            .Match(value => value, error => Withheld<Entered?>(error, ref failure));
        Entered? phone = failure is null
            ? Read(IdentifierKind.Phone, request.Phone, "phone")
                .Match(value => value, error => Withheld<Entered?>(error, ref failure))
            : null;
        Entered? corporate = failure is null
            ? Read(IdentifierKind.Email, request.CorporateEmail, "corporateEmail")
                .Match(value => value, error => Withheld<Entered?>(error, ref failure))
            : null;

        if (failure is not null)
        {
            return Result.Failure<Bound>(failure);
        }

        // REG-MAIL-001: an integrated invitation sends its link to the personal email
        // and provisions the corporate one, so it names both, and not as one address;
        // a value that does not read stays the identifier's refusal above.
        if (integrated)
        {
            if (email is null
                || (corporate is not null && string.Equals(email.Canonical, corporate.Canonical, StringComparison.Ordinal)))
            {
                return Result.Failure<Bound>(Named(ErrorCodes.InvitationAddressRequired, "email"));
            }

            if (corporate is null)
            {
                return Result.Failure<Bound>(Named(ErrorCodes.InvitationAddressRequired, "corporateEmail"));
            }
        }
        else if (corporate is not null)
        {
            return Result.Failure<Bound>(Malformed("corporateEmail"));
        }

        if ((corporate ?? email)?.Address is EmailAddress member
            && await locks.RefusedInAsync(organization, member, cancellationToken).ConfigureAwait(false)
                is Error outside)
        {
            return Result.Failure<Bound>(outside);
        }

        return Result.Success(new Bound(
            new InvitedIdentifiers(email?.Value, phone?.Value, corporate?.Value),
            email?.Address,
            corporate?.Address));
    }

    // REG-MAIL-003 and chapter 09 section 8a: a former mailbox is named with a reason,
    // free text of 1 to 1024 characters after trimming (API-CONV-002), and a reason is
    // part of that pair, so one without it is not the shape the endpoint takes.
    private static Result<MailboxTakeover?> Takeover(InvitationRequest request)
    {
        if (request.FormerMailbox is not FormerMailbox choice)
        {
            return request.Reason is null
                ? Result.Success<MailboxTakeover?>(null)
                : Result.Failure<MailboxTakeover?>(Malformed("reason"));
        }

        return request.Reason?.Trim() is { Length: > 0 and <= 1024 } stated
            ? Result.Success<MailboxTakeover?>(new MailboxTakeover(choice, stated))
            : Result.Failure<MailboxTakeover?>(Malformed("reason"));
    }

    // REG-INV-001: the roles attach across the organization with the membership, so
    // naming one is granting it: it asks what a grant asks, including the permission to
    // administer the deployment for a role that carries it (OPS-CFG-007).
    private async ValueTask<Result<IReadOnlyList<RoleName>>> RolesAsync(
        AccessContext context,
        OrganizationId organization,
        IReadOnlyList<RoleName>? named,
        CancellationToken cancellationToken)
    {
        if (named is null)
        {
            return Result.Failure<IReadOnlyList<RoleName>>(Malformed("roles"));
        }

        List<RoleName> distinct = [.. named.Distinct()];

        if (distinct.Count is 0)
        {
            return Result.Success<IReadOnlyList<RoleName>>(distinct);
        }

        if (await RefusedAsync(context, Permissions.GrantManage, organization, cancellationToken)
                .ConfigureAwait(false)
            is Error refused)
        {
            return Result.Failure<IReadOnlyList<RoleName>>(refused);
        }

        bool administering = false;

        foreach (RoleName role in distinct)
        {
            if (await roles.FindAsync(role, cancellationToken).ConfigureAwait(false) is not DefinedRole defined)
            {
                return Result.Failure<IReadOnlyList<RoleName>>(Malformed("roles"));
            }

            administering |= defined.Permissions.Contains(Permissions.SystemAdminister);
        }

        if (administering
            && await scope.RefusedAsync(context, Permissions.SystemAdminister, cancellationToken)
                .ConfigureAwait(false)
            is Error withheld)
        {
            return Result.Failure<IReadOnlyList<RoleName>>(withheld);
        }

        return Result.Success<IReadOnlyList<RoleName>>(distinct);
    }

    // REG-INV-001: each document is shown at the version current when the invitation
    // was issued, so a later publication changes nothing about what the person
    // acknowledges.
    private async ValueTask<Result<IReadOnlyList<InvitationDocument>>> DocumentsAsync(
        IReadOnlyList<string>? named,
        CancellationToken cancellationToken)
    {
        if (named is null)
        {
            return Result.Failure<IReadOnlyList<InvitationDocument>>(Malformed("documents"));
        }

        var shown = new List<InvitationDocument>(named.Count);

        foreach (string document in named.Distinct(StringComparer.Ordinal))
        {
            if (string.IsNullOrWhiteSpace(document))
            {
                return Result.Failure<IReadOnlyList<InvitationDocument>>(Malformed("documents"));
            }

            Error? failure = null;

            DocumentVersion current = (await legal.ReadAsync(document, version: null, cancellationToken)
                    .ConfigureAwait(false))
                .Match(value => value, error => Withheld<DocumentVersion>(error, ref failure));

            if (failure is not null)
            {
                return Result.Failure<IReadOnlyList<InvitationDocument>>(Malformed("documents"));
            }

            shown.Add(new InvitationDocument(current.DocumentName, current.Version));
        }

        return Result.Success<IReadOnlyList<InvitationDocument>>(shown);
    }

    // REG-MAIL-001: one mailbox stands for an address, and one invitation over it. A
    // mailbox an account holds is taken; an invitation still open over it is taken
    // too, until it is revoked; one that expired unacknowledged is replaced.
    // REG-MAIL-003, D-178: a mailbox someone has held passes on only under the choice
    // the invitation names; an address whose last holder was erased is found no more,
    // so it is one never held, and a choice where no held mailbox stands is refused.
    private async ValueTask<Result<Reservation>> ReservedAsync(
        EmailAddress corporate,
        MailboxTakeover? takeover,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (await mailboxes.FindAsync(corporate, cancellationToken).ConfigureAwait(false) is not Mailbox existing)
        {
            return takeover is null
                ? Result.Success(new Reservation(Mailbox.Reserved(corporate, now), IsNew: true, [], Former: null))
                : Result.Failure<Reservation>(Named(ErrorCodes.RequestInvalid, "formerMailbox"));
        }

        if (existing.IsHeld)
        {
            return Result.Failure<Reservation>(Malformed("corporateEmail"));
        }

        IReadOnlyList<Invitation> standing = await invitations
            .ReservingAsync(existing.Id, cancellationToken)
            .ConfigureAwait(false);

        if (standing.Any(invitation => !invitation.HasExpired(now)))
        {
            return Result.Failure<Reservation>(Malformed("corporateEmail"));
        }

        if (!existing.WasHeld)
        {
            return takeover is null
                ? Result.Success(new Reservation(existing, IsNew: false, standing, Former: null))
                : Result.Failure<Reservation>(Named(ErrorCodes.RequestInvalid, "formerMailbox"));
        }

        return takeover?.Choice switch
        {
            null => Result.Failure<Reservation>(Error.From(ErrorCodes.InvitationMailboxHeld)),
            FormerMailbox.Transfer => Result.Success(new Reservation(existing, IsNew: false, standing, Former: null)),
            _ => Result.Success(new Reservation(Mailbox.Reserved(corporate, now), IsNew: true, standing, Former: existing)),
        };
    }

    private async ValueTask ReserveAsync(
        Reservation reservation,
        SubjectId acting,
        string? breakGlassReason,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        foreach (Invitation replaced in reservation.Replaced)
        {
            await WithdrawnAsync(replaced, acting, breakGlassReason, now, cancellationToken).ConfigureAwait(false);
        }

        // REG-MAIL-003, D-178: the old mailbox stands aside, owed its removal, before
        // the new one takes the address.
        if (reservation.Former is Mailbox former)
        {
            former.Replace(now);

            await mailboxes.RecordAsync(former, cancellationToken).ConfigureAwait(false);
        }

        if (reservation.IsNew)
        {
            await mailboxes.AddAsync(reservation.Mailbox, cancellationToken).ConfigureAwait(false);

            return;
        }

        reservation.Mailbox.Reserve();

        await mailboxes.RecordAsync(reservation.Mailbox, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException">The context is absent.</exception>
    public async ValueTask<Result<AttachedInvitation>> AttachedAsync(
        AccessContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Effective is not SubjectId invitee)
        {
            return Result.Failure<AttachedInvitation>(Error.From(ErrorCodes.Denied));
        }

        if (await invitations.AttachedToAsync(invitee, cancellationToken).ConfigureAwait(false)
            is not Invitation invitation)
        {
            return Result.Failure<AttachedInvitation>(Error.From(ErrorCodes.InvitationNotFound));
        }

        OrganizationStanding standing = await directory
                .FindAsync(invitation.Organization, cancellationToken)
                .ConfigureAwait(false)
            ?? throw new InvalidOperationException("The invitation's organization has no row.");

        // The person is shown who invited them by the name that account shows, and by
        // nothing of theirs the account does not show (REG-INV-002).
        HeldProfile inviter = await accounts.ProfileAsync(invitation.Inviter, cancellationToken)
            .ConfigureAwait(false);

        return Result.Success(new AttachedInvitation(
            invitation.Id,
            invitation.Organization,
            standing.Name,
            inviter.DisplayName?.Value,
            invitation.Roles,
            invitation.Documents,
            invitation.ExpiresAt));
    }

    /// <summary>
    /// Forgets what every invitation that expired unused bound, leaving who invited
    /// into what and the mailbox it reserved (PRIV-RIGHT-005a, REG-MAIL-001).
    /// </summary>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>How many were swept.</returns>
    public async ValueTask<int> SweepAsync(CancellationToken cancellationToken)
    {
        (await work.BeginAsync(cancellationToken).ConfigureAwait(false))
            .Switch(() => { }, error => throw new InvalidOperationException(error.Code.ToString()));

        int swept = await invitations
            .SweepAsync(time.GetUtcNow(), cancellationToken)
            .ConfigureAwait(false);

        (await work.CommitAsync(cancellationToken).ConfigureAwait(false))
            .Switch(() => { }, error => throw new InvalidOperationException(error.Code.ToString()));

        return swept;
    }

    /// <inheritdoc/>
    public ValueTask<Result> EndMembershipAsync(
        AccessContext context,
        OrganizationId organization,
        SubjectId member,
        string source,
        CancellationToken cancellationToken) =>
        end.EndAsync(context, organization, member, source, cancellationToken);

    /// <inheritdoc/>
    public ValueTask<Result> AcknowledgeAsync(
        AccessContext context,
        InvitationId invitation,
        string source,
        CancellationToken cancellationToken) =>
        acknowledgement.AcknowledgeAsync(context, invitation, source, cancellationToken);

    private async ValueTask WithdrawnAsync(
        Invitation invitation,
        SubjectId acting,
        string? breakGlassReason,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        invitation.Revoke(now);

        await invitations.RecordAsync(invitation, cancellationToken).ConfigureAwait(false);
        await audit
            .InvitationChangedAsync(
                AuditActions.InvitationRevoked,
                invitation.Organization,
                invitation.Id,
                takeover: null,
                acting,
                breakGlassReason,
                now,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async ValueTask<Error?> SentAsync(
        EmailAddress linked,
        OpaqueToken token,
        string source,
        CancellationToken cancellationToken)
    {
        // IDN-ATTR-001: the person holds no account whose language is known, and the
        // request is the administrator's, so the link goes out in every language the
        // deployment declares.
        return (await sending
                .SendAsync(
                    new SendRequest(
                        SendDestination.Of(linked),
                        MessageKind.InvitationLink,
                        RestrictionPurpose.Notification,
                        source,
                        Language: null)
                    {
                        Values = new Dictionary<string, string>(capacity: 1, StringComparer.Ordinal)
                        {
                            ["token"] = token.Value,
                        },
                    },
                    cancellationToken)
                .ConfigureAwait(false))
            .Match(_ => (Error?)null, error => error);
    }

    private async ValueTask<Error?> RefusedAsync(
        AccessContext context,
        Permission permission,
        OrganizationId organization,
        CancellationToken cancellationToken) =>
        (await gate
            .RequireAsync(context, permission, organization, cancellationToken)
            .ConfigureAwait(false))
            .Match<Error?>(() => null, error => error);

    private sealed record Entered(string Value, string Canonical, EmailAddress? Address);

    private sealed record Bound(InvitedIdentifiers Identifiers, EmailAddress? Linked, EmailAddress? Corporate);

    private sealed record Reservation(Mailbox Mailbox, bool IsNew, IReadOnlyList<Invitation> Replaced, Mailbox? Former);
}
