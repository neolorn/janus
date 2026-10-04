using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Accounts;
using Janus.Authentication.Factors;
using Janus.Authentication.Identifiers;
using Janus.Authentication.Mailboxes;
using Janus.Authentication.Organizations;
using Janus.Authentication.Passwords;
using Janus.Authentication.Policies;
using Janus.Authentication.Sending;
using Janus.Authentication.Sessions;
using Janus.Core;
using Janus.Core.Configuration;

namespace Janus.Authentication.Invitations;

/// <summary>
/// The membership step's acknowledgement: the membership attaches, the roles are
/// granted, and where the organization's mail is integrated the corporate address
/// becomes the primary email and its mailbox the person's.
/// </summary>
/// <param name="gate">What judges whether the inviter may still grant what the invitation carries.</param>
/// <param name="scope">What judges the inviter's permissions in the administrative organization.</param>
/// <param name="roles">Where the roles the invitation names are read.</param>
/// <param name="restriction">Whether the account's processing is restricted, as the gate answers it.</param>
/// <param name="invitations">Where the invitation is read and its acknowledgement recorded.</param>
/// <param name="directory">Where the organization's standing is read.</param>
/// <param name="identifiers">Where the account's identifiers are read and the corporate address taken on.</param>
/// <param name="authenticators">Where the account's credentials are read.</param>
/// <param name="passwords">Where the account's password is read.</param>
/// <param name="policies">What resolves the policy the account holds once the membership attaches.</param>
/// <param name="locks">What judges the address the member will sign in with against the organization's lock.</param>
/// <param name="memberships">Where the membership and its grants are written.</param>
/// <param name="mailboxes">Where the corporate mailbox is given to the person.</param>
/// <param name="sessions">Where the account's live sessions are downgraded as the membership attaches.</param>
/// <param name="sending">What tells the security-notice set of the corporate address.</param>
/// <param name="events">Where the membership, the corporate address added and the new primary are announced.</param>
/// <param name="configuration">Where the membership limit, the email maximum and the languages are read.</param>
/// <param name="audit">Where the acknowledgement is written down.</param>
/// <param name="work">The one transaction the acknowledgement runs in.</param>
/// <param name="time">The clock the deployment runs on.</param>
/// <remarks>
/// Implements REG-INV-001, REG-INV-002, REG-MAIL-001, REG-DOM-001, IDN-LIFE-009a,
/// IDN-LIFE-009b, IDN-MEM-002, INT-MAIL-006 and chapter 09 section 6a. Nothing of the organization is
/// granted until the account meets its credential policy counting only the factors that
/// policy permits, which is also what makes every other factor stop signing in once the
/// membership attaches (IDN-LIFE-009b). Everything is written in one transaction, and
/// the invitation forgets what it bound in it.
/// </remarks>
internal sealed class InvitationAcknowledgement(
    IAccessGate gate,
    AdministrativeScope scope,
    IRoleCatalogue roles,
    ISettingsRestriction restriction,
    IInvitationStore invitations,
    IOrganizationDirectory directory,
    IIdentifierDirectory identifiers,
    IAuthenticatorStore authenticators,
    IPasswordStore passwords,
    PolicyResolution policies,
    DomainLock locks,
    IMembershipAttachment memberships,
    IMailboxStore mailboxes,
    ISessionStore sessions,
    INotificationHandler sending,
    IEvents events,
    IConfigurationStore configuration,
    IOrganizationAudit audit,
    IUnitOfWork work,
    TimeProvider time)
{
    private static readonly IReadOnlyDictionary<string, string> Nothing =
        new Dictionary<string, string>(capacity: 0, StringComparer.Ordinal);

    /// <summary>
    /// Acknowledges an invitation attached to the signed-in person's account.
    /// </summary>
    /// <param name="context">Who is signed in.</param>
    /// <param name="id">The invitation the membership step showed.</param>
    /// <param name="source">The address the request came from, which a notice counts against.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>Success, or the refusal of <see cref="IInvitations.AcknowledgeAsync"/>.</returns>
    /// <exception cref="ArgumentNullException">A part is absent.</exception>
    public async ValueTask<Result> AcknowledgeAsync(
        AccessContext context,
        InvitationId id,
        string source,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(source);

        if (context.Effective is not SubjectId invitee)
        {
            return Result.Failure(Error.From(ErrorCodes.Denied));
        }

        // IDN-ACCT-007 AC2: a membership changes the account, so a restricted account is
        // refused before anything is read or written.
        if (await restriction.RefusedAsync(context, cancellationToken).ConfigureAwait(false) is Error restricted)
        {
            return Result.Failure(restricted);
        }

        // An invitation attached to another account is answered as one that does not
        // exist, so nothing is learned of anyone else's.
        if (await invitations.FindAsync(id, cancellationToken).ConfigureAwait(false) is not Invitation invitation
            || invitation.Invitee != invitee)
        {
            return Result.Failure(Error.From(ErrorCodes.InvitationNotFound));
        }

        DateTimeOffset now = time.GetUtcNow();

        // An organization on its way out takes no new members, as it takes no new
        // invitations.
        if (!invitation.Stands
            || invitation.HasExpired(now)
            || invitation.Identifiers is not InvitedIdentifiers bound
            || await directory.FindAsync(invitation.Organization, cancellationToken).ConfigureAwait(false)
                is not { DeletionRequestedAt: null, ErasedAt: null })
        {
            return Result.Failure(Error.From(ErrorCodes.InvitationExpired));
        }

        if (await InviterLapsedAsync(invitation, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure(Error.From(ErrorCodes.InvitationExpired));
        }

        HeldIdentifiers held = await identifiers.HeldAsync(invitee, cancellationToken).ConfigureAwait(false);

        if (await MismatchedAsync(bound, held, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure(Error.From(ErrorCodes.InvitationIdentifierMismatch));
        }

        if (await OutsideLockAsync(invitation, bound, held, cancellationToken).ConfigureAwait(false)
            is Error outside)
        {
            return Result.Failure(outside);
        }

        Error? failure = null;

        Policy policy = (await policies.ForAsync(invitee, invitation.Organization, cancellationToken)
                .ConfigureAwait(false))
            .Match(value => value, error => Withheld<Policy>(error, ref failure));

        bool multiple = failure is null
            && (await configuration
                    .ReadAsync(Settings.OrganizationMultipleMemberships, cancellationToken).ConfigureAwait(false))
                .Match(value => value, error => Withheld<bool>(error, ref failure));

        int maximum = failure is null
            ? (await configuration
                    .ReadAsync(Settings.IdentifiersEmailMax, cancellationToken).ConfigureAwait(false))
                .Match(value => value, error => Withheld<int>(error, ref failure))
            : 0;

        if (failure is not null)
        {
            return Result.Failure(failure);
        }

        // 09 section 6a: a refusal no enrolment could meet is told before the credential
        // policy, so nobody is sent to enrol for a membership they cannot take. The
        // attachment judges the limit again under the account's lock.
        if (await memberships.RefusedAsync(invitee, invitation.Organization, multiple, cancellationToken)
                .ConfigureAwait(false)
            is Error limited)
        {
            return Result.Failure(limited);
        }

        bool corporate = invitation.Mailbox is not null && bound.CorporateEmail is not null;

        // The maximum counts every email the account holds, verified or not, as adding
        // any other does (REG-IDENT-002).
        if (corporate && held.OfKind(IdentifierKind.Email).Count >= maximum)
        {
            return Result.Failure(Error.From(ErrorCodes.IdentifierMaximum));
        }

        if (await UnmetAsync(invitee, policy, cancellationToken).ConfigureAwait(false) is Error enrol)
        {
            return Result.Failure(enrol);
        }

        if ((await work.BeginAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(() => null, error => error) is Error notBegun)
        {
            return Result.Failure(notBegun);
        }

        // D-166 X3: the invitation is read again under its lock, so a revocation
        // committed meanwhile stops the membership, and of two acknowledgements at once
        // only the first attaches.
        Invitation? standing = await invitations.FindForUpdateAsync(id, cancellationToken).ConfigureAwait(false);

        // D-166 X3: the organization is read again under its lock, so a deletion
        // requested or an erasure executed meanwhile takes no new member; one that waits
        // for this to commit ends the membership with the others.
        if (standing is not null
            && await directory.HoldAsync(standing.Organization, cancellationToken).ConfigureAwait(false)
                is not { DeletionRequestedAt: null, ErasedAt: null })
        {
            await work.RollbackAsync().ConfigureAwait(false);

            return Result.Failure(Error.From(ErrorCodes.InvitationExpired));
        }

        if (standing is null || standing.Invitee != invitee || !standing.Stands)
        {
            ErrorCode refused = standing is null || standing.Invitee != invitee
                ? ErrorCodes.InvitationNotFound
                : ErrorCodes.InvitationExpired;

            await work.RollbackAsync().ConfigureAwait(false);

            return Result.Failure(Error.From(refused));
        }

        invitation = standing;

        MembershipId membership = (await memberships
                .AttachAsync(
                    invitee,
                    invitation.Organization,
                    invitation.Documents,
                    invitation.Roles,
                    invitation.Inviter,
                    Reason(invitation.Id),
                    multiple,
                    now,
                    cancellationToken)
                .ConfigureAwait(false))
            .Match(value => value, error => Withheld<MembershipId>(error, ref failure));

        if (failure is not null)
        {
            await work.RollbackAsync().ConfigureAwait(false);

            return Result.Failure(failure);
        }

        // AUTH-SESS-009, IDN-LIFE-009b: the membership tightens the policy in force for
        // the account, so every session it holds is downgraded in this transaction and
        // passes no gate until a factor the organization permits is presented.
        _ = await sessions.DowngradeAsync(invitee, now, cancellationToken).ConfigureAwait(false);

        List<DomainEvent> announced =
        [
            new MembershipChanged(
                now,
                Key(membership, now),
                membership,
                invitation.Organization,
                MembershipChange.Began)
            {
                Subject = invitee,
            },
        ];

        if (corporate)
        {
            announced.AddRange(await CorporateAsync(
                    invitation,
                    bound,
                    held,
                    invitee,
                    maximum,
                    now,
                    source,
                    cancellationToken)
                .ConfigureAwait(false));
        }

        invitation.Acknowledge(now);

        await invitations.RecordAsync(invitation, cancellationToken).ConfigureAwait(false);
        await audit
            .InvitationChangedAsync(
                AuditActions.InvitationAcknowledged,
                invitation.Organization,
                invitation.Id,
                takeover: null,
                invitee,
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

    // The grants name where they came from in the machine's words, as a derivation's
    // do, and the words for a reader are the frontend's (CONV-CONTENT-001).
    private static string Reason(InvitationId invitation) =>
        string.Create(CultureInfo.InvariantCulture, $"invitation:{invitation.Value}");

    private static string Key(MembershipId membership, DateTimeOffset at) =>
        string.Create(CultureInfo.InvariantCulture, $"{membership.Value}@{at.UtcTicks}");

    private static string Key(IdentifierId identifier, DateTimeOffset at) =>
        string.Create(CultureInfo.InvariantCulture, $"{identifier.Value}@{at.UtcTicks}");

    private static string Canonical(IdentifierKind kind, string value) =>
        kind is IdentifierKind.Email
            ? EmailAddress.TryParse(value, out EmailAddress address)
                ? address.Value
                : throw new InvalidOperationException("An invitation binds a well-formed address.")
            : PhoneNumber.TryParse(value, out PhoneNumber number)
                ? number.Value
                : throw new InvalidOperationException("An invitation binds a well-formed number.");

    private static EmailAddress Address(string value) =>
        EmailAddress.TryParse(value, out EmailAddress address)
            ? address
            : throw new InvalidOperationException("An address bound or held is well-formed.");

    private static HeldIdentifier? Holding(HeldIdentifiers held, IdentifierKind kind, string value)
    {
        string canonical = Canonical(kind, value);

        return held.OfKind(kind).FirstOrDefault(identifier =>
            identifier.IsVerified && string.Equals(identifier.Canonical, canonical, StringComparison.Ordinal));
    }

    // AUTH-FACT-017 and chapter 10 section 1.1: the requirement the account is held
    // at, with no deadline, because no grace applies to an account joining.
    private static Error Enrol(PolicyField field, string value) =>
        new(
            ErrorCodes.StepUpRequired,
            new Dictionary<string, JsonElement>(capacity: 2, StringComparer.Ordinal)
            {
                ["outcome"] = JsonSerializer.SerializeToElement(WrittenName.Of(StepUpOutcome.Enrol)),
                ["policyRequirement"] = JsonSerializer.SerializeToElement(
                    new Dictionary<string, string>(capacity: 2, StringComparer.Ordinal)
                    {
                        ["field"] = WrittenName.Of(field),
                        ["value"] = value,
                    }),
            });

    private static SendDestination Destination(HeldIdentifier identifier) =>
        identifier.Kind is IdentifierKind.Email
            ? SendDestination.Of(EmailAddress.TryParse(identifier.Canonical, out EmailAddress address)
                ? address
                : throw new InvalidOperationException("A held address is canonical already."))
            : SendDestination.Of(PhoneNumber.TryParse(identifier.Canonical, out PhoneNumber number)
                ? number
                : throw new InvalidOperationException("A held number is canonical already."));

    private static TValue Withheld<TValue>(Error error, ref Error? failure)
    {
        failure = error;

        return default!;
    }

    // REG-INV-001, REG-INV-002: every identifier the invitation binds is one the
    // accepting account holds verified, and the corporate address the organization
    // asserts is nobody's yet.
    private async ValueTask<bool> MismatchedAsync(
        InvitedIdentifiers bound,
        HeldIdentifiers held,
        CancellationToken cancellationToken)
    {
        if ((bound.Email is string email && Holding(held, IdentifierKind.Email, email) is null)
            || (bound.Phone is string phone && Holding(held, IdentifierKind.Phone, phone) is null))
        {
            return true;
        }

        // The address the organization asserts is taken on at acknowledgement, so one
        // that any account holds by then, this one included, cannot be.
        return bound.CorporateEmail is string corporate
            && await identifiers
                    .OwnerAsync(IdentifierKind.Email, Canonical(IdentifierKind.Email, corporate), cancellationToken)
                    .ConfigureAwait(false)
                is not null;
    }

    // REG-INV-001: what the invitation attaches is granted by its inviter, so it is
    // judged against what the inviter holds now, as issuing it was: an inviter who no
    // longer manages the organization's memberships, or no longer may grant a role it
    // names, leaves an invitation that grants nothing.
    private async ValueTask<bool> InviterLapsedAsync(Invitation invitation, CancellationToken cancellationToken)
    {
        var inviter = AccessContext.Of(invitation.Inviter);

        if (await RefusedAsync(inviter, Permissions.MembershipManage, invitation.Organization, cancellationToken)
                .ConfigureAwait(false))
        {
            return true;
        }

        if (invitation.Roles.Count is 0)
        {
            return false;
        }

        if (await RefusedAsync(inviter, Permissions.GrantManage, invitation.Organization, cancellationToken)
                .ConfigureAwait(false))
        {
            return true;
        }

        bool administering = false;

        foreach (RoleName role in invitation.Roles)
        {
            administering |= (await roles.FindAsync(role, cancellationToken).ConfigureAwait(false))
                ?.Permissions.Contains(Permissions.SystemAdminister) is true;
        }

        return administering
            && await scope.RefusedAsync(inviter, Permissions.SystemAdminister, cancellationToken)
                    .ConfigureAwait(false)
                is not null;
    }

    private async ValueTask<bool> RefusedAsync(
        AccessContext inviter,
        Permission permission,
        OrganizationId organization,
        CancellationToken cancellationToken) =>
        (await gate
            .RequireAsync(inviter, permission, organization, cancellationToken)
            .ConfigureAwait(false))
            .Match(() => false, _ => true);

    // REG-DOM-001: the lock is judged as it now stands on the address the member will
    // sign in with: the corporate address where one is taken on, else the bound email,
    // else any verified email the account holds. An account holding no verified email
    // has no address a lock admits, so it is refused wherever the lock is on
    // (criterion 10).
    private async ValueTask<Error?> OutsideLockAsync(
        Invitation invitation,
        InvitedIdentifiers bound,
        HeldIdentifiers held,
        CancellationToken cancellationToken)
    {
        string? named = invitation.Mailbox is not null && bound.CorporateEmail is string corporate
            ? corporate
            : bound.Email;

        if (named is not null)
        {
            return await locks
                .RefusedInAsync(invitation.Organization, Address(named), cancellationToken)
                .ConfigureAwait(false);
        }

        Error? refused = await locks
            .RefusedWithoutAddressInAsync(invitation.Organization, cancellationToken)
            .ConfigureAwait(false);

        foreach (HeldIdentifier email in held.OfKind(IdentifierKind.Email).Where(identifier => identifier.IsVerified))
        {
            refused = await locks
                .RefusedInAsync(invitation.Organization, Address(email.Canonical), cancellationToken)
                .ConfigureAwait(false);

            if (refused is null)
            {
                return null;
            }
        }

        return refused;
    }

    // REG-INV-002 AC2: the account meets the organization's required assurance, and its
    // credential redundancy where that is enforced, with the factors the organization
    // permits and nothing else.
    private async ValueTask<Error?> UnmetAsync(
        SubjectId invitee,
        Policy policy,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<Authenticator> permitted =
        [
            .. (await authenticators.OfAsync(invitee, cancellationToken).ConfigureAwait(false))
                .Where(credential => policy.LoginFactors.Contains(credential.Factor)),
        ];

        bool password = policy.LoginFactors.Contains(FactorCatalogue.Password)
            && await passwords.FindAsync(invitee, cancellationToken).ConfigureAwait(false) is not null;

        if (StepUp.Reachable(HeldFactors.Of(permitted, password).Standing).Level < policy.RequiredAssurance)
        {
            return Enrol(PolicyField.RequiredAssurance, WrittenName.Of(policy.RequiredAssurance));
        }

        return policy.CredentialRedundancy is CredentialRedundancy.Enforced && !Redundancy.Satisfied(permitted)
            ? Enrol(PolicyField.CredentialRedundancy, WrittenName.Of(policy.CredentialRedundancy))
            : null;
    }

    // REG-MAIL-001: the corporate address becomes the primary email, the personal
    // email the invitation named stays verified beside it through the membership, the
    // mailbox becomes the person's and is owed enabled, and the set as it stood hears
    // of the address once (REG-IDENT-004).
    // REG-MAIL-001: the corporate address is added to the account as it becomes the
    // primary, so both are announced, each keyed by the address and the instant as an
    // added identifier is (entry 248 of D-166).
    private async ValueTask<DomainEvent[]> CorporateAsync(
        Invitation invitation,
        InvitedIdentifiers bound,
        HeldIdentifiers held,
        SubjectId invitee,
        int maximum,
        DateTimeOffset now,
        string source,
        CancellationToken cancellationToken)
    {
        // D-166 X3: the corporate address is taken on under the lock on the account's
        // identifiers, so a promotion the person makes at the same moment either comes
        // first and is moved, or waits and moves the role itself; never two primaries.
        await identifiers.HoldAsync(invitee, cancellationToken).ConfigureAwait(false);

        held = await identifiers.HeldAsync(invitee, cancellationToken).ConfigureAwait(false);

        string corporate = bound.CorporateEmail
            ?? throw new InvalidOperationException("The invitation names no corporate address.");
        HeldIdentifier personal = (bound.Email is string email ? Holding(held, IdentifierKind.Email, email) : null)
            ?? throw new InvalidOperationException("An integrated invitation names a personal email the account holds.");
        MailboxId reserved = invitation.Mailbox
            ?? throw new InvalidOperationException("The invitation reserved no mailbox.");
        Mailbox mailbox = await mailboxes.FindAsync(reserved, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("The reserved mailbox has no row.");

        var address = IdentifierId.New(time);

        await identifiers
            .TakeCorporateAsync(
                invitee,
                address,
                corporate,
                Canonical(IdentifierKind.Email, corporate),
                personal.Id,
                now,
                maximum,
                cancellationToken)
            .ConfigureAwait(false);

        mailbox.Hold(invitee);

        await mailboxes.RecordAsync(mailbox, cancellationToken).ConfigureAwait(false);
        _ = await TellAsync(held.NoticeSet, invitee, source, cancellationToken).ConfigureAwait(false);

        return
        [
            new IdentifierAdded(now, Key(address, now), address, IdentifierKind.Email)
            {
                Subject = invitee,
            },
            new IdentifierPrimaryChanged(now, Key(address, now), address, IdentifierKind.Email)
            {
                Subject = invitee,
            },
        ];
    }

    private async ValueTask<int> TellAsync(
        IReadOnlyList<HeldIdentifier> reached,
        SubjectId invitee,
        string source,
        CancellationToken cancellationToken)
    {
        string? settled = await identifiers.LanguageAsync(invitee, cancellationToken).ConfigureAwait(false);

        IReadOnlyList<string> languages = (await configuration
                .ReadAsync(Settings.NotificationLanguages, cancellationToken).ConfigureAwait(false))
            .Match(read => read, error => throw new InvalidOperationException(error.Code.ToString()));

        string? language = RecipientLanguage.Of(settled, requested: null, languages);
        int told = 0;

        foreach (HeldIdentifier identifier in reached)
        {
            var request = new SendRequest(
                Destination(identifier),
                MessageKind.IdentifierAdded,
                RestrictionPurpose.Notification,
                source,
                language)
            {
                Subject = invitee,
                Values = Nothing,
            };

            // A security notice one destination refuses still reaches the rest: the
            // set exists so that no one channel can silence it.
            Result<SendReference> sent = await sending
                .SendAsync(request, cancellationToken)
                .ConfigureAwait(false);

            told += sent.Match(_ => 1, _ => 0);
        }

        return told;
    }
}
