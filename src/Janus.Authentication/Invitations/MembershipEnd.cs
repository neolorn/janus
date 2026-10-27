using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.BreakGlass;
using Janus.Authentication.Factors;
using Janus.Authentication.Identifiers;
using Janus.Authentication.Mailboxes;
using Janus.Authentication.Organizations;
using Janus.Authentication.Sending;
using Janus.Core;
using Janus.Core.Configuration;

namespace Janus.Authentication.Invitations;

/// <summary>
/// The end of a membership by an administrator: the membership ends, the grants a person
/// gave the account there are revoked and the account leaves the organization's groups,
/// and where it gave the account a corporate address the address and its mailbox are
/// retired and the personal email becomes the primary.
/// </summary>
/// <param name="gate">The one place a permission is evaluated.</param>
/// <param name="stepUp">What judges the session against the <c>membership:end</c> gate.</param>
/// <param name="directory">Where the organization's standing is read.</param>
/// <param name="memberships">Where the membership is ended.</param>
/// <param name="revocations">Where the grants the end reaches are revoked and its groups left.</param>
/// <param name="emergency">Which account the break-glass session belongs to.</param>
/// <param name="identifiers">Where the account's identifiers are read and the corporate address retired.</param>
/// <param name="mailboxes">Where the mailbox the account holds is retired.</param>
/// <param name="sending">What tells the security-notice set of the new primary.</param>
/// <param name="events">Where the end, the corporate address removed and the new primary are announced.</param>
/// <param name="configuration">Where the languages are read.</param>
/// <param name="audit">Where the end is written down.</param>
/// <param name="work">The one transaction the end runs in.</param>
/// <param name="time">The clock the deployment runs on.</param>
/// <remarks>
/// Implements REG-MAIL-003, IDN-MEM-001, INT-MAIL-006a, OPS-BOOT-002 and chapter 09
/// section 8a (D-213, D-216). The account's state is left as it is: suspension is a
/// separate step of offboarding. Everything is written in one transaction, so no instant
/// exists at which the account holds no primary email, or holds a grant of an
/// organization it no longer belongs to.
/// </remarks>
internal sealed class MembershipEnd(
    IAccessGate gate,
    StepUpGuard stepUp,
    IOrganizationDirectory directory,
    IMembershipEnding memberships,
    IMembershipRevocations revocations,
    IEmergencyAccount emergency,
    IIdentifierDirectory identifiers,
    IMailboxStore mailboxes,
    IGovernedSend sending,
    IEvents events,
    IConfigurationStore configuration,
    IOrganizationAudit audit,
    IUnitOfWork work,
    TimeProvider time)
{
    private const string Ended = "membership-ended";

    private static readonly IReadOnlyDictionary<string, string> Nothing =
        new Dictionary<string, string>(capacity: 0, StringComparer.Ordinal);

    /// <summary>
    /// Ends an account's membership of an organization.
    /// </summary>
    /// <param name="context">Who is ending it.</param>
    /// <param name="session">The session the step-up is judged on.</param>
    /// <param name="organization">Of which organization.</param>
    /// <param name="member">Whose membership.</param>
    /// <param name="reason">Why, which each record the end writes carries.</param>
    /// <param name="source">The address the request came from, which a notice counts against.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>Success, or the refusal of <see cref="IInvitations.EndMembershipAsync"/>.</returns>
    /// <exception cref="ArgumentNullException">A part is absent.</exception>
    public async ValueTask<Result> EndAsync(
        AccessContext context,
        SessionId session,
        OrganizationId organization,
        SubjectId member,
        string reason,
        string source,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(reason);
        ArgumentNullException.ThrowIfNull(source);

        // A change is made by a person, whose identity the record carries.
        if (context.Acting is not SubjectId acting)
        {
            return Result.Failure(Error.From(ErrorCodes.Denied));
        }

        // IDN-MEM-001, 09 section 8: a path naming no organization is answered for the
        // organization, whichever organization the permission is asked in.
        if (await ScopeOfAsync(organization, cancellationToken).ConfigureAwait(false) is null)
        {
            return Result.Failure(Error.From(ErrorCodes.OrganizationNotFound));
        }

        if ((await gate
                    .RequireAsync(context, Permissions.MembershipManage, organization, cancellationToken)
                    .ConfigureAwait(false))
                .Match<Error?>(() => null, error => error) is Error refused)
        {
            return Result.Failure(refused);
        }

        // API-CONV-002, D-216: every record the end writes carries its reason, of 1 to 1024
        // characters after trimming.
        if (reason.Trim() is not { Length: > 0 and <= 1024 } stated)
        {
            return Result.Failure(
                Error.From(ErrorCodes.RequestMalformed, "member", JsonSerializer.SerializeToElement("reason")));
        }

        // IDN-MEM-001, X9 of D-166: a membership the account does not hold is told before
        // any transaction begins, and before a step-up is asked for what cannot happen.
        if (await memberships.FindAsync(member, organization, cancellationToken).ConfigureAwait(false) is null)
        {
            return Result.Failure(Error.From(ErrorCodes.MembershipNotFound));
        }

        if (await directory.FindAsync(organization, cancellationToken).ConfigureAwait(false)
            is not OrganizationStanding standing)
        {
            return Result.Failure(Error.From(ErrorCodes.OrganizationNotFound));
        }

        // OPS-BOOT-002, IDN-MEM-001 (D-202): the reserved account's grant confers only
        // while it is a member of the administrative organization, so that membership is
        // kept for anyone who asks, refused once the organization's standing is read and
        // before the step-up, as the revocation of its grant is.
        if (standing.IsAdministrative
            && await emergency.FindAsync(cancellationToken).ConfigureAwait(false) == member)
        {
            return Result.Failure(Error.From(ErrorCodes.Denied));
        }

        // AUTHZ-GRANT-005 (D-213): the end is judged as the revocations and removals it
        // makes are, before the step-up, and again inside the unit of work.
        if (await revocations
                .RefusedAsync(context, member, organization, standing.IsAdministrative, cancellationToken)
                .ConfigureAwait(false)
            is Error unassignable)
        {
            return Result.Failure(unassignable);
        }

        // REG-MAIL-003: ending a membership changes another person's account, so it is
        // the membership:end step-up action, judged after every other refusal (09
        // section 8).
        if (await stepUp
                .PassedAsync(acting, session, StepUpAction.MembershipEnd, cancellationToken)
                .ConfigureAwait(false)
            is Error challenged)
        {
            return Result.Failure(challenged);
        }

        DateTimeOffset now = time.GetUtcNow();

        if ((await work.BeginAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(_ => null, error => error) is Error notBegun)
        {
            return Result.Failure(notBegun);
        }

        // AUTHZ-GATE-006, D-183: the gate is asked again inside the unit of work, with the
        // acting account's row held before any other lock, so a restriction committed since
        // the gate step refuses the change before anything is written. A member ending
        // their own membership goes on to lock that row itself, so it is taken for the
        // change first.
        if (acting == member)
        {
            await identifiers.HoldAsync(member, cancellationToken).ConfigureAwait(false);
        }

        if ((await gate
                    .RequireAsync(context, Permissions.MembershipManage, organization, cancellationToken)
                    .ConfigureAwait(false))
                .Match<Error?>(() => null, error => error) is Error since)
        {
            await work.RollbackAsync().ConfigureAwait(false);

            return Result.Failure(since);
        }

        if (await revocations
                .RefusedAsync(context, member, organization, standing.IsAdministrative, cancellationToken)
                .ConfigureAwait(false)
            is Error unassignableSince)
        {
            await work.RollbackAsync().ConfigureAwait(false);

            return Result.Failure(unassignableSince);
        }

        // A second end that read the membership before this one committed finds none
        // here, and is answered as the find would answer it.
        if (await memberships.EndAsync(member, organization, now, cancellationToken).ConfigureAwait(false)
            is not MembershipId ended)
        {
            await work.RollbackAsync().ConfigureAwait(false);

            return Result.Failure(Error.From(ErrorCodes.MembershipNotFound));
        }

        // IDN-MEM-001 (D-213): in the transaction that ends the membership, recorded with
        // the end's reason and actor.
        await revocations
            .RevokeAsync(
                member,
                organization,
                standing.IsAdministrative,
                acting,
                stated,
                context.BreakGlassReason,
                now,
                cancellationToken)
            .ConfigureAwait(false);

        List<DomainEvent> announced =
        [
            new MembershipChanged(now, Key(ended, now), ended, organization, MembershipChange.Ended)
            {
                Subject = member,
            },
        ];

        // The mailboxes are the administrative organization's, so only the end of that
        // membership takes one back; another the account ends leaves it in place.
        if (standing.IsAdministrative
            && await mailboxes.HeldByAsync(member, cancellationToken).ConfigureAwait(false) is Mailbox mailbox)
        {
            announced.AddRange(await RetiredAsync(member, mailbox, now, source, cancellationToken)
                .ConfigureAwait(false));
        }

        await audit
            .MembershipEndedAsync(
                organization,
                ended,
                member,
                stated,
                acting,
                context.BreakGlassReason,
                now,
                cancellationToken)
            .ConfigureAwait(false);

        foreach (DomainEvent happened in announced)
        {
            if ((await events.PublishAsync(happened, cancellationToken).ConfigureAwait(false))
                .Match(() => (Error?)null, error => error) is Error unpublished)
            {
                await work.RollbackAsync().ConfigureAwait(false);

                return Result.Failure(unpublished);
            }
        }

        if ((await work.CommitAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(() => null, error => error) is Error notCommitted)
        {
            return Result.Failure(notCommitted);
        }

        return Result.Success();
    }

    /// <summary>
    /// Sets an account's membership of an organization to end at a later instant, or
    /// moves the instant set.
    /// </summary>
    /// <param name="context">Who is setting it.</param>
    /// <param name="session">The session the step-up is judged on.</param>
    /// <param name="organization">Of which organization.</param>
    /// <param name="member">Whose membership.</param>
    /// <param name="endsAt">The instant it ends.</param>
    /// <param name="reason">Why, which the end is recorded with.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>Success, or the refusal of <see cref="IInvitations.SetMembershipEndAsync"/>.</returns>
    /// <exception cref="ArgumentNullException">A part is absent.</exception>
    public ValueTask<Result> SetEndAsync(
        AccessContext context,
        SessionId session,
        OrganizationId organization,
        SubjectId member,
        DateTimeOffset endsAt,
        string reason,
        CancellationToken cancellationToken) =>
        ChangeEndAsync(context, session, organization, member, endsAt, reason, cancellationToken);

    /// <summary>
    /// Cancels the end an account's membership of an organization is set to reach.
    /// </summary>
    /// <param name="context">Who is cancelling it.</param>
    /// <param name="session">The session the step-up is judged on.</param>
    /// <param name="organization">Of which organization.</param>
    /// <param name="member">Whose membership.</param>
    /// <param name="reason">Why, which the cancellation is recorded with.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>Success, or the refusal of <see cref="IInvitations.CancelMembershipEndAsync"/>.</returns>
    /// <exception cref="ArgumentNullException">A part is absent.</exception>
    public ValueTask<Result> CancelEndAsync(
        AccessContext context,
        SessionId session,
        OrganizationId organization,
        SubjectId member,
        string reason,
        CancellationToken cancellationToken) =>
        ChangeEndAsync(context, session, organization, member, endsAt: null, reason, cancellationToken);

    // IDN-MEM-001 (D-213): an end set, moved or cancelled takes the permission, the
    // step-up and the refusals of an end now, judged when it is set; a cancellation is
    // an end set to no instant.
    private async ValueTask<Result> ChangeEndAsync(
        AccessContext context,
        SessionId session,
        OrganizationId organization,
        SubjectId member,
        DateTimeOffset? endsAt,
        string reason,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(reason);

        if (context.Acting is not SubjectId acting)
        {
            return Result.Failure(Error.From(ErrorCodes.Denied));
        }

        if (await ScopeOfAsync(organization, cancellationToken).ConfigureAwait(false) is null)
        {
            return Result.Failure(Error.From(ErrorCodes.OrganizationNotFound));
        }

        if ((await gate
                    .RequireAsync(context, Permissions.MembershipManage, organization, cancellationToken)
                    .ConfigureAwait(false))
                .Match<Error?>(() => null, error => error) is Error refused)
        {
            return Result.Failure(refused);
        }

        if (reason.Trim() is not { Length: > 0 and <= 1024 } stated)
        {
            return Result.Failure(
                Error.From(ErrorCodes.RequestMalformed, "member", JsonSerializer.SerializeToElement("reason")));
        }

        // 09 section 8a: an end is set to a later instant; one not after the present is
        // an end now, which is its own operation.
        if (endsAt is DateTimeOffset instant && instant <= time.GetUtcNow())
        {
            return Result.Failure(
                Error.From(ErrorCodes.RequestInvalid, "member", JsonSerializer.SerializeToElement("endsAt")));
        }

        if (await memberships.FindAsync(member, organization, cancellationToken).ConfigureAwait(false) is null)
        {
            return Result.Failure(Error.From(ErrorCodes.MembershipNotFound));
        }

        if (await directory.FindAsync(organization, cancellationToken).ConfigureAwait(false)
            is not OrganizationStanding standing)
        {
            return Result.Failure(Error.From(ErrorCodes.OrganizationNotFound));
        }

        if (standing.IsAdministrative
            && await emergency.FindAsync(cancellationToken).ConfigureAwait(false) == member)
        {
            return Result.Failure(Error.From(ErrorCodes.Denied));
        }

        // 09 section 8a: a cancellation finds an end set, or there is nothing to cancel.
        if (endsAt is null
            && await memberships.EndSetAsync(member, organization, cancellationToken).ConfigureAwait(false) is null)
        {
            return Result.Failure(Error.From(ErrorCodes.MembershipNotFound));
        }

        if (await revocations
                .RefusedAsync(context, member, organization, standing.IsAdministrative, cancellationToken)
                .ConfigureAwait(false)
            is Error unassignable)
        {
            return Result.Failure(unassignable);
        }

        if (await stepUp
                .PassedAsync(acting, session, StepUpAction.MembershipEnd, cancellationToken)
                .ConfigureAwait(false)
            is Error challenged)
        {
            return Result.Failure(challenged);
        }

        DateTimeOffset now = time.GetUtcNow();

        if ((await work.BeginAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(_ => null, error => error) is Error notBegun)
        {
            return Result.Failure(notBegun);
        }

        // AUTHZ-GATE-006, D-183: as for an end now.
        if ((await gate
                    .RequireAsync(context, Permissions.MembershipManage, organization, cancellationToken)
                    .ConfigureAwait(false))
                .Match<Error?>(() => null, error => error) is Error since)
        {
            await work.RollbackAsync().ConfigureAwait(false);

            return Result.Failure(since);
        }

        if (await revocations
                .RefusedAsync(context, member, organization, standing.IsAdministrative, cancellationToken)
                .ConfigureAwait(false)
            is Error unassignableSince)
        {
            await work.RollbackAsync().ConfigureAwait(false);

            return Result.Failure(unassignableSince);
        }

        // An end at the same moment, or a cancellation that came first, leaves nothing
        // here, and is answered as the find would answer it.
        MembershipId? changed = endsAt is DateTimeOffset set
            ? await memberships.SetEndAsync(member, organization, set, acting, stated, cancellationToken)
                .ConfigureAwait(false)
            : await memberships.CancelEndAsync(member, organization, cancellationToken).ConfigureAwait(false);

        if (changed is not MembershipId membership)
        {
            await work.RollbackAsync().ConfigureAwait(false);

            return Result.Failure(Error.From(ErrorCodes.MembershipNotFound));
        }

        await audit
            .MembershipEndSetAsync(
                organization,
                membership,
                member,
                endsAt,
                stated,
                acting,
                context.BreakGlassReason,
                now,
                cancellationToken)
            .ConfigureAwait(false);

        if ((await work.CommitAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(() => null, error => error) is Error notCommitted)
        {
            return Result.Failure(notCommitted);
        }

        return Result.Success();
    }

    // CONV-DESIGN-002 AC3: the organization the path names is where the gate is asked,
    // and resolving it, read alone, is part of the gate step.
    private async ValueTask<OrganizationId?> ScopeOfAsync(
        OrganizationId organization,
        CancellationToken cancellationToken) =>
        await directory.FindAsync(organization, cancellationToken).ConfigureAwait(false) is null
            ? null
            : organization;

    private static string Key(MembershipId membership, DateTimeOffset at) =>
        string.Create(CultureInfo.InvariantCulture, $"{Ended}:{membership.Value}@{at.UtcTicks}");

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

    // REG-MAIL-003: the corporate address stops being the account's and the personal
    // email the membership kept becomes the primary in the same step; the mailbox is
    // retired, which leaves it owed disabled and ends every app password with it
    // (INT-MAIL-006a); and the set as it now stands hears of the new primary once
    // (REG-IDENT-005). The address that left and the new primary are both announced
    // (entry 251 of D-166).
    private async ValueTask<DomainEvent[]> RetiredAsync(
        SubjectId member,
        Mailbox mailbox,
        DateTimeOffset now,
        string source,
        CancellationToken cancellationToken)
    {
        // D-166 X3: as for the corporate address's taking on.
        await identifiers.HoldAsync(member, cancellationToken).ConfigureAwait(false);

        HeldIdentifiers held = await identifiers.HeldAsync(member, cancellationToken).ConfigureAwait(false);
        IdentifierId retired = held.OfKind(IdentifierKind.Email)
                .FirstOrDefault(identifier =>
                    string.Equals(identifier.Canonical, mailbox.Address.Value, StringComparison.Ordinal))
                ?.Id
            ?? throw new InvalidOperationException("A held mailbox's address is held by its holder.");

        IdentifierId primary = await identifiers
            .RetireCorporateAsync(member, mailbox.Address.Value, cancellationToken)
            .ConfigureAwait(false);

        mailbox.Retire(now);

        await mailboxes.RecordAsync(mailbox, cancellationToken).ConfigureAwait(false);

        // The set as it now stands is the set as it stood less the address that left:
        // the personal email was in it already, and no backup setting changed. The
        // retired address behaves as unknown, so it is told nothing.
        _ = await TellAsync(
                [
                    .. held.NoticeSet.Where(identifier =>
                        identifier.Kind is not IdentifierKind.Email
                        || !string.Equals(identifier.Canonical, mailbox.Address.Value, StringComparison.Ordinal)),
                ],
                member,
                source,
                cancellationToken)
            .ConfigureAwait(false);

        return
        [
            new IdentifierRemoved(now, Key(retired, now), retired, IdentifierKind.Email)
            {
                Subject = member,
            },
            new IdentifierPrimaryChanged(now, Key(primary, now), primary, IdentifierKind.Email)
            {
                Subject = member,
            },
        ];
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
                MessageKind.IdentifierSettingsChanged,
                RestrictionPurpose.Notification,
                source,
                language)
            {
                Subject = member,
                Values = Nothing,
            };

            // A security notice one destination refuses still reaches the rest: the
            // set exists so that no one channel can silence it.
            Result<SendReference> sent = await sending
                .UndertakeAsync(request, cancellationToken)
                .ConfigureAwait(false);

            told += sent.Match(_ => 1, _ => 0);
        }

        return told;
    }
}
