using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Factors;
using Janus.Authentication.Identifiers;
using Janus.Authentication.Mailboxes;
using Janus.Authentication.Organizations;
using Janus.Authentication.Passwords;
using Janus.Authentication.Policies;
using Janus.Authentication.Sending;
using Janus.Core;
using Janus.Core.Configuration;

namespace Janus.Authentication.Invitations;

/// <summary>
/// The membership step's acknowledgement: the membership attaches, the roles are
/// granted, and where the organization's mail is integrated the corporate address
/// becomes the primary email and its mailbox the person's.
/// </summary>
/// <param name="invitations">Where the invitation is read and its acknowledgement recorded.</param>
/// <param name="directory">Where the organization's standing is read.</param>
/// <param name="identifiers">Where the account's identifiers are read and the corporate address taken on.</param>
/// <param name="authenticators">Where the account's credentials are read.</param>
/// <param name="passwords">Where the account's password is read.</param>
/// <param name="policies">What resolves the policy the account holds once the membership attaches.</param>
/// <param name="memberships">Where the membership and its grants are written.</param>
/// <param name="mailboxes">Where the corporate mailbox is given to the person.</param>
/// <param name="sending">What tells the security-notice set of the corporate address.</param>
/// <param name="events">Where the membership and the new primary are announced.</param>
/// <param name="configuration">Where the membership limit, the email maximum and the languages are read.</param>
/// <param name="audit">Where the acknowledgement is written down.</param>
/// <param name="work">The one transaction the acknowledgement runs in.</param>
/// <param name="time">The clock the deployment runs on.</param>
/// <remarks>
/// Implements REG-INV-001, REG-INV-002, REG-MAIL-001, IDN-LIFE-009a, IDN-LIFE-009b,
/// IDN-MEM-002, INT-MAIL-006 and chapter 09 section 6a. Nothing of the organization is
/// granted until the account meets its credential policy counting only the factors that
/// policy permits, which is also what makes every other factor stop signing in once the
/// membership attaches (IDN-LIFE-009b). Everything is written in one transaction, and
/// the invitation forgets what it bound in it.
/// </remarks>
internal sealed class InvitationAcknowledgement(
    IInvitationStore invitations,
    IOrganizationDirectory directory,
    IIdentifierDirectory identifiers,
    IAuthenticatorStore authenticators,
    IPasswordStore passwords,
    PolicyResolution policies,
    IMembershipAttachment memberships,
    IMailboxStore mailboxes,
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

        HeldIdentifiers held = await identifiers.HeldAsync(invitee, cancellationToken).ConfigureAwait(false);

        if (await MismatchedAsync(bound, held, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure(Error.From(ErrorCodes.InvitationIdentifierMismatch));
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

        if (await UnmetAsync(invitee, policy, cancellationToken).ConfigureAwait(false) is Error enrol)
        {
            return Result.Failure(enrol);
        }

        bool corporate = invitation.Mailbox is not null && bound.CorporateEmail is not null;

        // The maximum counts every email the account holds, verified or not, as adding
        // any other does (REG-IDENT-002).
        if (corporate && held.OfKind(IdentifierKind.Email).Count >= maximum)
        {
            return Result.Failure(Error.From(ErrorCodes.IdentifierMaximum));
        }

        await work.BeginAsync(cancellationToken).ConfigureAwait(false);

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
            return Result.Failure(failure);
        }

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
            announced.Add(await CorporateAsync(
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
                invitee,
                now,
                cancellationToken)
            .ConfigureAwait(false);

        foreach (DomainEvent happened in announced)
        {
            if ((await events.PublishAsync(happened, cancellationToken).ConfigureAwait(false))
                .Match(() => (Error?)null, error => error) is Error unpublished)
            {
                return Result.Failure(unpublished);
            }
        }

        await work.CommitAsync(cancellationToken).ConfigureAwait(false);

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

    private static HeldIdentifier? Holding(HeldIdentifiers held, IdentifierKind kind, string value)
    {
        string canonical = Canonical(kind, value);

        return held.OfKind(kind).FirstOrDefault(identifier =>
            identifier.IsVerified && string.Equals(identifier.Canonical, canonical, StringComparison.Ordinal));
    }

    private static Error Enrol(PolicyField field, string value) =>
        new(
            ErrorCodes.StepUpRequired,
            new Dictionary<string, JsonElement>(capacity: 3, StringComparer.Ordinal)
            {
                ["outcome"] = JsonSerializer.SerializeToElement(WrittenName.Of(StepUpOutcome.Enrol)),
                ["field"] = JsonSerializer.SerializeToElement(WrittenName.Of(field)),
                ["value"] = JsonSerializer.SerializeToElement(value),
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
    private async ValueTask<DomainEvent> CorporateAsync(
        Invitation invitation,
        InvitedIdentifiers bound,
        HeldIdentifiers held,
        SubjectId invitee,
        int maximum,
        DateTimeOffset now,
        string source,
        CancellationToken cancellationToken)
    {
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

        return new IdentifierPrimaryChanged(now, Key(address, now), address, IdentifierKind.Email)
        {
            Subject = invitee,
        };
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
            .Match(read => read, _ => (IReadOnlyList<string>)[]);

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
