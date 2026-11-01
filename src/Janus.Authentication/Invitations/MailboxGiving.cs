using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Accounts;
using Janus.Authentication.BreakGlass;
using Janus.Authentication.Factors;
using Janus.Authentication.Identifiers;
using Janus.Authentication.Mailboxes;
using Janus.Authentication.Organizations;
using Janus.Authentication.Sending;
using Janus.Authentication.Sessions;
using Janus.Core;
using Janus.Core.Configuration;

namespace Janus.Authentication.Invitations;

/// <summary>
/// Gives a current member of the administrative organization a mailbox with no
/// invitation.
/// </summary>
/// <param name="gate">The one place a permission is evaluated.</param>
/// <param name="stepUp">What judges the session against the <c>invitation:issue</c> gate.</param>
/// <param name="directory">Where the organization's standing is read.</param>
/// <param name="memberships">Where the member's current membership is read.</param>
/// <param name="emergency">Which account is the reserved emergency account.</param>
/// <param name="accounts">Where the member's state is read.</param>
/// <param name="identifiers">Where the member's emails are read and the address taken on.</param>
/// <param name="locks">Where the organization's domain lock is judged.</param>
/// <param name="reservations">The rules the mailbox is reserved under.</param>
/// <param name="mailboxes">Where the mailbox the member holds is read and the new one held.</param>
/// <param name="inUse">The mail server in use, where the deployment has one.</param>
/// <param name="sending">What tells the security-notice set of the address.</param>
/// <param name="events">Where the address added and a new primary are announced.</param>
/// <param name="configuration">Where the email maximum and the languages are read.</param>
/// <param name="audit">Where the giving, and an adoption, are written down.</param>
/// <param name="work">The one transaction the giving runs in.</param>
/// <param name="time">The clock the deployment runs on.</param>
/// <remarks>
/// Implements REG-MAIL-001 AC9 and AC10, REG-MAIL-003, INT-MAIL-006, OPS-BOOT-002 and
/// chapter 09 section 8a (D-253, D-255). The mailbox is reserved as an invitation's
/// corporate address is, under the same rules and codes, and held by the member at once,
/// so it is owed <c>enabled</c>. The address is taken on as a verified email; the
/// member's other emails stay, with no lock. It hands out no role, so AUTHZ-GRANT-005 does
/// not bear on it. Everything is written in one transaction; a refused request writes
/// nothing.
/// </remarks>
internal sealed class MailboxGiving(
    IAccessGate gate,
    StepUpGuard stepUp,
    IOrganizationDirectory directory,
    IMembershipEnding memberships,
    IEmergencyAccount emergency,
    IAccountDirectory accounts,
    IIdentifierDirectory identifiers,
    DomainLock locks,
    MailboxReservations reservations,
    IMailboxStore mailboxes,
    IMailServerInUse inUse,
    IGovernedSend sending,
    IEvents events,
    IConfigurationStore configuration,
    IOrganizationAudit audit,
    IUnitOfWork work,
    TimeProvider time)
{
    private const string AddressMember = "address";

    private static readonly IReadOnlyDictionary<string, string> Nothing =
        new Dictionary<string, string>(capacity: 0, StringComparer.Ordinal);

    /// <summary>
    /// Gives the member a mailbox.
    /// </summary>
    /// <param name="context">Who is giving it.</param>
    /// <param name="session">The session the step-up is judged on.</param>
    /// <param name="organization">The administrative organization.</param>
    /// <param name="member">Whose it becomes.</param>
    /// <param name="request">The address, what becomes of a mailbox it had, and why.</param>
    /// <param name="source">The address the request came from, which the notice counts against.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The mailbox, or the refusal of <see cref="IInvitations.GiveMailboxAsync"/>.</returns>
    /// <exception cref="ArgumentNullException">A part is absent.</exception>
    public async ValueTask<Result<GivenMailbox>> GiveAsync(
        AccessContext context,
        SessionId session,
        OrganizationId organization,
        SubjectId member,
        MailboxGift request,
        string source,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(source);

        FormerMailbox? formerMailbox = request.FormerMailbox;

        // A mailbox is given by a person, whose identity the record carries.
        if (context.Acting is not SubjectId acting)
        {
            return Result.Failure<GivenMailbox>(Error.From(ErrorCodes.Denied));
        }

        // REG-MAIL-001 (D-253): the route is the administrative organization's alone, and
        // any other organization is answered as a member it does not hold is, so nothing
        // is disclosed.
        if (await ScopeOfAsync(organization, cancellationToken).ConfigureAwait(false) is null)
        {
            return Result.Failure<GivenMailbox>(Error.From(ErrorCodes.MembershipNotFound));
        }

        if (await RefusedAsync(context, organization, cancellationToken).ConfigureAwait(false) is Error refused)
        {
            return Result.Failure<GivenMailbox>(refused);
        }

        // API-CONV-002: the giving is recorded with its reason, of 1 to 1024 characters
        // after trimming.
        if (request.Reason?.Trim() is not { Length: > 0 and <= 1024 } stated)
        {
            return Result.Failure<GivenMailbox>(Named(ErrorCodes.RequestMalformed, "reason"));
        }

        // OPS-BOOT-002: the reserved account holds no mailbox, refused before the step-up.
        if (await emergency.FindAsync(cancellationToken).ConfigureAwait(false) == member)
        {
            return Result.Failure<GivenMailbox>(Error.From(ErrorCodes.Denied));
        }

        if (!inUse.Chosen().Match(_ => true, _ => false))
        {
            return Result.Failure<GivenMailbox>(Error.From(
                ErrorCodes.MailServerUnavailable,
                "requires",
                JsonSerializer.SerializeToElement(Settings.IntegrationMailServerEndpoint.Key.ToString())));
        }

        Error? failure = await UngivenAsync(organization, member, cancellationToken).ConfigureAwait(false);

        if (failure is not null)
        {
            return Result.Failure<GivenMailbox>(failure);
        }

        EmailAddress given = Read(request.Address ?? string.Empty)
            .Match(value => value, error => Withheld<EmailAddress>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure<GivenMailbox>(failure);
        }

        if (await locks.RefusedInAsync(organization, given, cancellationToken).ConfigureAwait(false) is Error locked)
        {
            return Result.Failure<GivenMailbox>(locked);
        }

        DateTimeOffset now = time.GetUtcNow();

        MailboxReservation reservation = (await JudgedAsync(member, given, formerMailbox, now, cancellationToken)
                .ConfigureAwait(false))
            .Match(value => value, error => Withheld<MailboxReservation>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure<GivenMailbox>(failure);
        }

        int maximum = (await configuration
                .ReadAsync(Settings.IdentifiersEmailMax, cancellationToken)
                .ConfigureAwait(false))
            .Match(value => value, error => Withheld<int>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure<GivenMailbox>(failure);
        }

        if (Filled(await identifiers.HeldAsync(member, cancellationToken).ConfigureAwait(false), maximum))
        {
            return Result.Failure<GivenMailbox>(Error.From(ErrorCodes.IdentifierMaximum));
        }

        // REG-MAIL-001 (D-253): giving a mailbox changes another person's account, so it
        // is the step-up the invitation's issue asks, judged after every other refusal.
        if (await stepUp
                .PassedAsync(acting, session, StepUpAction.InvitationIssue, cancellationToken)
                .ConfigureAwait(false)
            is Error challenged)
        {
            return Result.Failure<GivenMailbox>(challenged);
        }

        if ((await work.BeginAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(_ => null, error => error) is Error notBegun)
        {
            return Result.Failure<GivenMailbox>(notBegun);
        }

        Result<GivenMailbox> outcome = await GivenAsync(
                context,
                acting,
                organization,
                member,
                given,
                formerMailbox,
                stated,
                maximum,
                now,
                source,
                cancellationToken)
            .ConfigureAwait(false);

        if (outcome.Match(_ => (Error?)null, error => error) is Error unwritten)
        {
            await work.RollbackAsync().ConfigureAwait(false);

            return Result.Failure<GivenMailbox>(unwritten);
        }

        if ((await work.CommitAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(() => null, error => error) is Error notCommitted)
        {
            return Result.Failure<GivenMailbox>(notCommitted);
        }

        return outcome;
    }

    private static Error Named(ErrorCode code, string member) =>
        Error.From(code, "member", JsonSerializer.SerializeToElement(member));

    private static TValue Withheld<TValue>(Error error, ref Error? failure)
    {
        failure = error;

        return default!;
    }

    // The address as the request names it, read as an invitation's corporate address is.
    private static Result<EmailAddress> Read(string entered)
    {
        if (!EmailAddress.TryParse(entered.Trim(), out EmailAddress given))
        {
            return Result.Failure<EmailAddress>(Named(ErrorCodes.IdentifierInvalid, AddressMember));
        }

        return ScriptMixing.IsSingleScriptPerWord(given.Value)
            ? Result.Success(given)
            : Result.Failure<EmailAddress>(Named(ErrorCodes.IdentifierMixedScript, AddressMember));
    }

    // D-253: the address counts against identifiers.email.max, every email the account
    // holds counted, verified or not, as an invitation's acknowledgement counts them.
    private static bool Filled(HeldIdentifiers held, int maximum) =>
        held.OfKind(IdentifierKind.Email).Count >= maximum;

    // REG-MAIL-001 (D-253): the address becomes the primary email where the account holds
    // none from the address's domain, and leaves the primary where it holds one.
    private static bool Promoted(HeldIdentifiers held, EmailAddress given) =>
        !held.OfKind(IdentifierKind.Email).Any(email => email.IsVerified && SameDomain(email.Canonical, given.Value));

    private static bool SameDomain(string held, string given) =>
        string.Equals(held[(held.LastIndexOf('@') + 1)..], given[(given.LastIndexOf('@') + 1)..], StringComparison.Ordinal);

    private static string Key(IdentifierId identifier, DateTimeOffset at) =>
        string.Create(CultureInfo.InvariantCulture, $"{identifier.Value}@{at.UtcTicks}");

    private static SendDestination Destination(HeldIdentifier identifier) =>
        identifier.Kind is IdentifierKind.Email
            ? SendDestination.Of(EmailAddress.TryParse(identifier.Canonical, out EmailAddress address)
                ? address
                : throw new InvalidOperationException("A held address is canonical already."))
            : SendDestination.Of(PhoneNumber.TryParse(identifier.Canonical, out PhoneNumber number)
                ? number
                : throw new InvalidOperationException("A held number is canonical already."));

    // CONV-DESIGN-002 AC3: the organization the path names is where the gate is asked,
    // and resolving it, read alone, is part of the gate step; the route is the
    // administrative organization's alone.
    private async ValueTask<OrganizationId?> ScopeOfAsync(
        OrganizationId organization,
        CancellationToken cancellationToken) =>
        await directory.FindAsync(organization, cancellationToken).ConfigureAwait(false) is { IsAdministrative: true }
            ? organization
            : null;

    private async ValueTask<Error?> RefusedAsync(
        AccessContext context,
        OrganizationId organization,
        CancellationToken cancellationToken) =>
        (await gate
            .RequireAsync(context, Permissions.MembershipManage, organization, cancellationToken)
            .ConfigureAwait(false))
            .Match<Error?>(() => null, error => error);

    // REG-MAIL-001 (D-253): a current member, active or restricted, who holds no mailbox.
    private async ValueTask<Error?> UngivenAsync(
        OrganizationId organization,
        SubjectId member,
        CancellationToken cancellationToken)
    {
        if (await memberships.FindAsync(member, organization, cancellationToken).ConfigureAwait(false) is null)
        {
            return Error.From(ErrorCodes.MembershipNotFound);
        }

        if (await accounts.StateAsync(member, cancellationToken).ConfigureAwait(false)
            is not (AccountState.Active or AccountState.Restricted))
        {
            return Error.From(ErrorCodes.AccountStateConflict);
        }

        return await mailboxes.HeldByAsync(member, cancellationToken).ConfigureAwait(false) is null
            ? null
            : Error.From(ErrorCodes.MailboxHeld);
    }

    // D-253: an address another account holds as an email, or that a removal keeps for
    // another account's undo, is taken; then the mailbox is judged as an invitation's.
    private async ValueTask<Result<MailboxReservation>> JudgedAsync(
        SubjectId member,
        EmailAddress given,
        FormerMailbox? formerMailbox,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        SubjectId? owner = await identifiers.OwnerAsync(IdentifierKind.Email, given.Value, cancellationToken)
            .ConfigureAwait(false);
        SubjectId? reserved = await identifiers.ReservedToAsync(IdentifierKind.Email, given.Value, now, cancellationToken)
            .ConfigureAwait(false);

        if (owner is not null || (reserved is SubjectId other && other != member))
        {
            return Result.Failure<MailboxReservation>(Named(ErrorCodes.MailboxTaken, AddressMember));
        }

        return await reservations
            .ReservedAsync(given, formerMailbox, AddressMember, now, cancellationToken)
            .ConfigureAwait(false);
    }

    private async ValueTask<Result<GivenMailbox>> GivenAsync(
        AccessContext context,
        SubjectId acting,
        OrganizationId organization,
        SubjectId member,
        EmailAddress given,
        FormerMailbox? formerMailbox,
        string stated,
        int maximum,
        DateTimeOffset now,
        string source,
        CancellationToken cancellationToken)
    {
        // AUTHZ-GATE-006, D-183: the gate is asked again inside the unit of work, with
        // the acting account's row held before any other lock.
        if (acting == member)
        {
            await identifiers.HoldAsync(member, cancellationToken).ConfigureAwait(false);
        }

        if (await RefusedAsync(context, organization, cancellationToken).ConfigureAwait(false) is Error since)
        {
            return Result.Failure<GivenMailbox>(since);
        }

        // D-166 X3: the address is taken on under the lock on the account's identifiers
        // and on the value, and every rule is judged again under them.
        if (acting != member)
        {
            await identifiers.HoldAsync(member, cancellationToken).ConfigureAwait(false);
        }

        await identifiers.LockValuesAsync([(IdentifierKind.Email, given.Value)], cancellationToken).ConfigureAwait(false);

        if (await UngivenAsync(organization, member, cancellationToken).ConfigureAwait(false) is Error ungiven)
        {
            return Result.Failure<GivenMailbox>(ungiven);
        }

        Error? failure = null;

        MailboxReservation reservation = (await JudgedAsync(member, given, formerMailbox, now, cancellationToken)
                .ConfigureAwait(false))
            .Match(value => value, error => Withheld<MailboxReservation>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure<GivenMailbox>(failure);
        }

        HeldIdentifiers held = await identifiers.HeldAsync(member, cancellationToken).ConfigureAwait(false);

        if (Filled(held, maximum))
        {
            return Result.Failure<GivenMailbox>(Error.From(ErrorCodes.IdentifierMaximum));
        }

        await reservations
            .ReserveAsync(reservation, acting, context.BreakGlassReason, now, cancellationToken)
            .ConfigureAwait(false);

        Mailbox mailbox = reservation.Mailbox;

        mailbox.Hold(member);

        await mailboxes.RecordAsync(mailbox, cancellationToken).ConfigureAwait(false);

        var identifier = IdentifierId.New(time);
        bool promoted = Promoted(held, given);

        await identifiers
            .TakeOnAsync(member, identifier, IdentifierKind.Email, given.Value, given.Value, now, cancellationToken)
            .ConfigureAwait(false);

        if (promoted)
        {
            await identifiers.PromoteAsync(member, identifier, cancellationToken).ConfigureAwait(false);
        }

        await audit
            .MailboxGivenAsync(
                organization,
                mailbox.Id,
                member,
                stated,
                formerMailbox,
                acting,
                context.BreakGlassReason,
                now,
                cancellationToken)
            .ConfigureAwait(false);

        // REG-MAIL-003 (D-255): taking over an account the mail server holds is recorded
        // with the giving's reason.
        if (formerMailbox is FormerMailbox.Adopt)
        {
            await audit
                .MailboxAdoptedAsync(
                    organization,
                    mailbox.Id,
                    member,
                    stated,
                    acting,
                    context.BreakGlassReason,
                    now,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        // REG-IDENT-004, D-253: the set as it stood before hears of the address once.
        _ = await TellAsync(held.NoticeSet, member, source, cancellationToken).ConfigureAwait(false);

        List<DomainEvent> announced =
        [
            new IdentifierAdded(now, Key(identifier, now), identifier, IdentifierKind.Email)
            {
                Subject = member,
            },
        ];

        if (promoted)
        {
            announced.Add(new IdentifierPrimaryChanged(now, Key(identifier, now), identifier, IdentifierKind.Email)
            {
                Subject = member,
            });
        }

        foreach (DomainEvent happened in announced)
        {
            if ((await events.PublishAsync(happened, cancellationToken).ConfigureAwait(false))
                .Match(() => (Error?)null, error => error) is Error unpublished)
            {
                return Result.Failure<GivenMailbox>(unpublished);
            }
        }

        return Result.Success(new GivenMailbox(mailbox.Id, mailbox.Address, mailbox.Owed(stands: true)));
    }

    private async ValueTask<int> TellAsync(
        IReadOnlyList<HeldIdentifier> reached,
        SubjectId member,
        string source,
        CancellationToken cancellationToken)
    {
        string? settled = await identifiers.LanguageAsync(member, cancellationToken).ConfigureAwait(false);

        IReadOnlyList<string> languages = (await configuration
                .ReadAsync(Settings.NotificationLanguages, cancellationToken).ConfigureAwait(false))
            .Match(read => read, error => throw new InvalidOperationException(error.Code.ToString()));

        string? language = RecipientLanguage.Of(settled, requested: null, languages);
        int told = 0;

        foreach (HeldIdentifier identifier in reached)
        {
            var request = new OutboundMessage(
                Destination(identifier),
                MessageKind.IdentifierAdded,
                RestrictionPurpose.Notification,
                source,
                language)
            {
                Subject = member,
                Values = Nothing,
            };

            // A security notice one destination refuses still reaches the rest: the set
            // exists so that no one channel can silence it.
            Result<SendReference> sent = await sending
                .UndertakeAsync(request, cancellationToken)
                .ConfigureAwait(false);

            told += sent.Match(_ => 1, _ => 0);
        }

        return told;
    }
}
