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
/// The invitations an organization issues into its membership, and their revocation.
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
/// <param name="mailboxes">Where the corporate mailboxes are reserved.</param>
/// <param name="server">
/// The mail server the administrative organization's mail is integrated with, absent
/// where the deployment registered none.
/// </param>
/// <param name="sending">What carries the link.</param>
/// <param name="configuration">Where the lifetime, the phone setting and the languages are read.</param>
/// <param name="audit">Where every issue and revocation is written down.</param>
/// <param name="work">The one transaction an operation runs in.</param>
/// <param name="time">The clock the deployment runs on.</param>
/// <param name="randomness">Where the link's token is drawn from.</param>
/// <remarks>
/// Implements LIB-API-005, IDN-LIFE-009a, REG-INV-001, REG-MAIL-001, INT-MAIL-006 and
/// chapter 09 section 8a. The mailboxes are the administrative organization's, so its
/// mail is integrated exactly where it is the organization invited into and the
/// deployment registered a mail server. The link goes out before anything is written,
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
    IMailboxStore mailboxes,
    IMailServer? server,
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

        if (await RefusedAsync(context, Permissions.MembershipManage, organization, cancellationToken)
                .ConfigureAwait(false)
            is Error refused)
        {
            return Result.Failure<IssuedInvitation>(refused);
        }

        // An organization on its way out takes no new members.
        if (await directory.FindAsync(organization, cancellationToken).ConfigureAwait(false)
            is not { DeletionRequestedAt: null } standing)
        {
            return Result.Failure<IssuedInvitation>(Malformed("id"));
        }

        bool integrated = standing.IsAdministrative && server is not null;
        Error? failure = null;

        Bound bound = (await BoundAsync(organization, request, integrated, cancellationToken)
                .ConfigureAwait(false))
            .Match(value => value, error => Withheld<Bound>(error, ref failure));

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

        if (bound.Corporate is string corporate)
        {
            reservation = (await ReservedAsync(corporate, now, cancellationToken).ConfigureAwait(false))
                .Match(value => value, error => Withheld<Reservation>(error, ref failure));

            if (failure is not null)
            {
                return Result.Failure<IssuedInvitation>(failure);
            }
        }

        if (await stepUp
                .PassedAsync(acting, session, StepUpAction.InvitationIssue, cancellationToken)
                .ConfigureAwait(false)
            is Error challenged)
        {
            return Result.Failure<IssuedInvitation>(challenged);
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

        await work.BeginAsync(cancellationToken).ConfigureAwait(false);

        if (reservation is not null)
        {
            await ReserveAsync(reservation, acting, now, cancellationToken).ConfigureAwait(false);
        }

        await invitations.AddAsync(invitation, cancellationToken).ConfigureAwait(false);
        await audit
            .InvitationChangedAsync(
                AuditActions.InvitationIssued,
                organization,
                invitation.Id,
                acting,
                now,
                cancellationToken)
            .ConfigureAwait(false);
        await work.CommitAsync(cancellationToken).ConfigureAwait(false);

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

        await work.BeginAsync(cancellationToken).ConfigureAwait(false);
        await WithdrawnAsync(held, acting, now, cancellationToken).ConfigureAwait(false);

        // REG-MAIL-001: a reservation nobody ever took is given up with the invitation
        // that made it; a mailbox someone has held stays, disabled.
        if (held.Mailbox is MailboxId reserved
            && await mailboxes.FindAsync(reserved, cancellationToken).ConfigureAwait(false)
                is { IsRemovable: true } mailbox)
        {
            mailbox.Release(now);

            await mailboxes.RecordAsync(mailbox, cancellationToken).ConfigureAwait(false);
        }

        await work.CommitAsync(cancellationToken).ConfigureAwait(false);

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

        if (integrated)
        {
            if (email is null
                || (corporate is not null && string.Equals(email.Canonical, corporate.Canonical, StringComparison.Ordinal)))
            {
                return Result.Failure<Bound>(Named(ErrorCodes.IdentifierInvalid, "email"));
            }

            if (corporate is null)
            {
                return Result.Failure<Bound>(Named(ErrorCodes.IdentifierInvalid, "corporateEmail"));
            }
        }
        else if (corporate is not null)
        {
            return Result.Failure<Bound>(Malformed("corporateEmail"));
        }

        if (phone is not null)
        {
            AttributeRequirement collected = (await configuration
                    .ReadAsync(Settings.RegistrationPhone, cancellationToken).ConfigureAwait(false))
                .Match(value => value, error => Withheld<AttributeRequirement>(error, ref failure));

            if (failure is not null)
            {
                return Result.Failure<Bound>(failure);
            }

            // A phone the deployment does not collect is a field no registration takes.
            if (collected is AttributeRequirement.Off)
            {
                return Result.Failure<Bound>(Malformed("phone"));
            }
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
            corporate?.Canonical));
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

    // REG-MAIL-001: one mailbox per address, and one invitation standing over it. A
    // mailbox an account holds is taken; an invitation still open over it is taken
    // too, until it is revoked; one that expired unacknowledged is replaced.
    private async ValueTask<Result<Reservation>> ReservedAsync(
        string corporate,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (await mailboxes.FindAsync(corporate, cancellationToken).ConfigureAwait(false) is not Mailbox existing)
        {
            return Result.Success(new Reservation(Mailbox.Reserved(corporate, now), IsNew: true, []));
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

        return Result.Success(new Reservation(existing, IsNew: false, standing));
    }

    private async ValueTask ReserveAsync(
        Reservation reservation,
        SubjectId acting,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        foreach (Invitation replaced in reservation.Replaced)
        {
            await WithdrawnAsync(replaced, acting, now, cancellationToken).ConfigureAwait(false);
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
    /// <exception cref="ArgumentNullException">A part is absent.</exception>
    public async ValueTask<Result> OpenAsync(
        AccessContext context,
        string token,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(token);

        if (context.Effective is not SubjectId invitee)
        {
            return Result.Failure(Error.From(ErrorCodes.Denied));
        }

        DateTimeOffset now = time.GetUtcNow();

        // IDN-LIFE-009a AC2: the link is single use, so the account that presses it is
        // the one it attaches to, and every token that opens nothing is answered alike.
        Invitation? invitation = await invitations
            .FindByTokenAsync(OpaqueToken.Of(token).Fingerprint(), cancellationToken)
            .ConfigureAwait(false);

        if (invitation is null || !invitation.Opens(now))
        {
            return Result.Failure(Error.From(ErrorCodes.InvitationExpired));
        }

        await work.BeginAsync(cancellationToken).ConfigureAwait(false);

        invitation.AttachTo(invitee, now);

        await invitations.RecordAsync(invitation, cancellationToken).ConfigureAwait(false);
        await work.CommitAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
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

    private async ValueTask WithdrawnAsync(
        Invitation invitation,
        SubjectId acting,
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
                acting,
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

    private sealed record Bound(InvitedIdentifiers Identifiers, EmailAddress? Linked, string? Corporate);

    private sealed record Reservation(Mailbox Mailbox, bool IsNew, IReadOnlyList<Invitation> Replaced);
}
