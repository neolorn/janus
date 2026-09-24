using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Alerting;
using Janus.Authentication.Identifiers;
using Janus.Authentication.Invitations;
using Janus.Authentication.Mailboxes;
using Janus.Authentication.Organizations;
using Janus.Authentication.Recovery;
using Janus.Authentication.Registration;
using Janus.Core;
using Janus.Core.Configuration;

namespace Janus.Authentication.Bootstrap;

/// <summary>
/// What stands a fresh deployment up: its named values, its administrative roles and
/// organization, its first administrator with an enrolment link, the reserved
/// <c>emergency</c> account and the restore test's canary subject.
/// </summary>
/// <param name="seed">Where the deployment is seeded.</param>
/// <param name="audit">Where the administrative organization's creation is recorded.</param>
/// <param name="memberships">Where each account joins the administrative organization.</param>
/// <param name="identifiers">Where the administrator takes on the corporate address.</param>
/// <param name="mailboxes">Where the administrator's mailbox is queued.</param>
/// <param name="links">Where the enrolment link is held.</param>
/// <param name="alerts">Where the alert that no emergency credential exists is raised.</param>
/// <param name="configuration">Where the values bootstrap reads are.</param>
/// <param name="work">The one transaction bootstrap runs in.</param>
/// <param name="randomness">Where subject identifiers and the link's token come from.</param>
/// <param name="time">The clock the deployment runs on.</param>
/// <remarks>
/// Implements OPS-BOOT-001, OPS-BOOT-002, INT-MAIL-006 AC1a and AC1b, DR-007,
/// IDN-PRIN-001, chapter 10 sections 3 and 4.1a, and D-133. There is no gate: whoever reaches the database
/// already holds more than the first account will (D-028), and what stands in for one
/// is that nothing runs while a system administrator exists. No break-glass credential
/// is made here; the management application issues it (OPS-BOOT-004), and until it
/// does the alert raised here keeps saying so.
/// </remarks>
internal sealed class DeploymentBootstrap(
    IDeploymentSeed seed,
    IOrganizationAudit audit,
    IMembershipAttachment memberships,
    IIdentifierDirectory identifiers,
    IMailboxStore mailboxes,
    IRecoveryLinkStore links,
    IRaisedAlerts alerts,
    IConfigurationStore configuration,
    IUnitOfWork work,
    RandomNumberGenerator randomness,
    TimeProvider time)
{
    // What each account bootstrap creates records as the reason for what it was
    // granted, where no person stated one.
    private const string Reason = "OPS-BOOT-001";

    // An address under the name RFC 2606 reserves for what can never resolve, so no
    // message addressed to the canary reaches anybody.
    private const string CanaryAddress = "canary@restore-test.invalid";

    private const string CanaryName = "Restore canary";

    private static readonly RoleName SystemAdministrator = RoleName.Parse("system-administrator");

    // IDN-PRIN-001: nobody is signed in while bootstrap runs, so what it defines and
    // sets is recorded under a principal of its own that may do nothing else.
    private static readonly SystemPrincipal Principal =
        SystemPrincipal.ForDeployment("bootstrap", Reason, SystemOperation.Bootstrap);

    // Chapter 10 section 3: the three administrative roles seeded so the system is
    // usable at once.
    private static readonly Dictionary<RoleName, IReadOnlyList<Permission>> Roles = new()
    {
        [SystemAdministrator] = Permissions.All,
        [RoleName.Parse("auditor")] = [Permissions.AuditRead, Permissions.GrantRead, Permissions.RecordsOfProcessingRead],
        [RoleName.Parse("support")] = [Permissions.RestrictionGrant, Permissions.AuditRead],
    };

    /// <summary>
    /// Stands the deployment up, or refuses to where it stands already.
    /// </summary>
    /// <param name="request">What the command line named.</param>
    /// <param name="cancellationToken">Abandons bootstrap, which then leaves nothing behind.</param>
    /// <returns>
    /// The enrolment link issued the administrator, or the failure naming what was
    /// refused.
    /// </returns>
    /// <exception cref="ArgumentNullException">The request is absent.</exception>
    public async ValueTask<Result<BootstrapEnrolment>> RunAsync(
        BootstrapRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (Stated(request.Organization) is not string organizationName)
        {
            return Result.Failure<BootstrapEnrolment>(Malformed("organization"));
        }

        Error? failure = null;

        Entered email = Read(IdentifierKind.Email, request.Email, "email")
            .Match(value => value, error => Withheld<Entered>(error, ref failure));
        Entered? phone = failure is null
            ? Read(IdentifierKind.Phone, request.Phone, "phone")
                .Match(value => value, error => Withheld<Entered>(error, ref failure))
            : null;
        Entered? mailbox = failure is null && request.Mailbox is string corporate
            ? Read(IdentifierKind.Email, corporate, "mailbox")
                .Match(value => value, error => Withheld<Entered>(error, ref failure))
            : null;

        if (failure is not null)
        {
            return Result.Failure<BootstrapEnrolment>(failure);
        }

        // REG-MAIL-001: the personal email is where the person is reached, so it is
        // never the address the deployment creates for them.
        if (mailbox is not null && string.Equals(mailbox.Canonical, email.Canonical, StringComparison.Ordinal))
        {
            return Result.Failure<BootstrapEnrolment>(Named(ErrorCodes.IdentifierInvalid, "mailbox"));
        }

        DateTimeOffset now = time.GetUtcNow();

        await work.BeginAsync(cancellationToken).ConfigureAwait(false);

        // OPS-BOOT-001 AC1: a deployment that has a system administrator is stood up.
        if (await seed.AdministeredAsync(cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure<BootstrapEnrolment>(Error.From(ErrorCodes.Denied));
        }

        await seed.ConfigureAsync(request.Named, Principal, now, cancellationToken).ConfigureAwait(false);

        TimeSpan recency = (await configuration.ReadAsync(Settings.SessionStepUpRecency, cancellationToken).ConfigureAwait(false))
            .Match(value => value, error => Withheld<TimeSpan>(error, ref failure));
        TimeSpan lifetime = failure is null
            ? (await configuration.ReadAsync(Settings.RecoveryLinkLifetime, cancellationToken).ConfigureAwait(false))
                .Match(value => value, error => Withheld<TimeSpan>(error, ref failure))
            : default;
        int maximum = failure is null
            ? (await configuration.ReadAsync(Settings.IdentifiersEmailMax, cancellationToken).ConfigureAwait(false))
                .Match(value => value, error => Withheld<int>(error, ref failure))
            : default;
        IReadOnlyList<string> origins = failure is null
            ? (await configuration.ReadAsync(Settings.WebAuthnOrigins, cancellationToken).ConfigureAwait(false))
                .Match(value => value, error => Withheld<IReadOnlyList<string>>(error, ref failure))
            : [];

        if (failure is not null)
        {
            return Result.Failure<BootstrapEnrolment>(failure);
        }

        if (origins.Count is 0 || !Uri.TryCreate(origins[0], UriKind.Absolute, out Uri? origin))
        {
            return Result.Failure<BootstrapEnrolment>(Malformed(Settings.WebAuthnOrigins.Key.ToString()));
        }

        await seed.DefineRolesAsync(Roles, Principal, now, cancellationToken).ConfigureAwait(false);

        var organization = OrganizationId.New(time);

        await seed.CreateOrganizationAsync(organization, organizationName, now, cancellationToken).ConfigureAwait(false);
        await audit
            .RecordedAsync(AuditActions.OrganizationCreated, organization, Principal, now, cancellationToken)
            .ConfigureAwait(false);

        // Chapter 10 section 4.1a: the administrative organization's policy is the one
        // the table gives it at bootstrap, and is changed from then on like any other.
        await seed
            .ConfigureAsync(
                new Dictionary<ConfigurationKey, string>
                {
                    [Settings.OrganizationPolicy.For(organization.ToString())] =
                        Settings.OrganizationPolicy.Write(Administrative(recency)),
                },
                Principal,
                now,
                cancellationToken)
            .ConfigureAwait(false);

        var administrator = SubjectId.New(randomness);
        var personal = IdentifierId.New(time);

        await seed
            .CreateAccountAsync(
                administrator,
                [Verified(personal, IdentifierKind.Email, email, now), Verified(IdentifierId.New(time), IdentifierKind.Phone, phone!, now)],
                name: null,
                now,
                cancellationToken)
            .ConfigureAwait(false);

        if (await JoinAsync(administrator, organization, [SystemAdministrator], now, cancellationToken).ConfigureAwait(false)
            is Error unjoined)
        {
            return Result.Failure<BootstrapEnrolment>(unjoined);
        }

        // INT-MAIL-006 AC1a: the membership is effective at once and the mailbox is only
        // queued, so the provisioning job creates it once the mail server is reachable.
        if (mailbox is not null)
        {
            await identifiers
                .TakeCorporateAsync(
                    administrator,
                    IdentifierId.New(time),
                    mailbox.Value,
                    mailbox.Canonical,
                    personal,
                    now,
                    maximum,
                    cancellationToken)
                .ConfigureAwait(false);

            var provisioned = Mailbox.Reserved(mailbox.Canonical, now);
            provisioned.Hold(administrator);

            await mailboxes.AddAsync(provisioned, cancellationToken).ConfigureAwait(false);
        }

        // OPS-BOOT-002 and INT-MAIL-006 AC1b: the reserved account holds the role and
        // nothing else: no identifier, no mailbox, no way in but the sealed credential.
        var emergency = SubjectId.New(randomness);

        await seed.CreateEmergencyAsync(emergency, now, cancellationToken).ConfigureAwait(false);

        if (await JoinAsync(emergency, organization, [SystemAdministrator], now, cancellationToken).ConfigureAwait(false)
            is Error unjoinedEmergency)
        {
            return Result.Failure<BootstrapEnrolment>(unjoinedEmergency);
        }

        // DR-007: one field encrypted under the canary's own subject key and one verified
        // email found by its fingerprint are what the restore test proves readable.
        var canary = SubjectId.New(randomness);

        await seed
            .CreateAccountAsync(
                canary,
                [Verified(IdentifierId.New(time), IdentifierKind.Email, Canary(), now)],
                CanaryDisplayName(),
                now,
                cancellationToken)
            .ConfigureAwait(false);

        if (await JoinAsync(canary, organization, [], now, cancellationToken).ConfigureAwait(false)
            is Error unjoinedCanary)
        {
            return Result.Failure<BootstrapEnrolment>(unjoinedCanary);
        }

        await seed
            .ConfigureAsync(
                new Dictionary<ConfigurationKey, string>
                {
                    [Settings.BackupRestoreTestCanary.Key] = Settings.BackupRestoreTestCanary.Write(canary.ToString()),
                },
                Principal,
                now,
                cancellationToken)
            .ConfigureAwait(false);

        var token = OpaqueToken.Draw(randomness);

        await links
            .ReplaceAsync(
                RecoveryLink.Issue(token, administrator, RecoveryPurpose.Enrolment, now, lifetime),
                cancellationToken)
            .ConfigureAwait(false);

        // OPS-BOOT-001 AC3 and D-133: the deployment has no emergency credential until the
        // management application issues one, and the alert says so from the start.
        await alerts
            .AddAsync(
                new RaisedAlert(RaisedAlertId.Of(now), Alerts.Of(AlertCondition.NoEmergencyCredential, scope: null, now)),
                cancellationToken)
            .ConfigureAwait(false);

        await work.CommitAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success(new BootstrapEnrolment(Enrolment(origin, token), now + lifetime));
    }

    private static string? Stated(string text) =>
        text?.Trim() is { Length: > 0 and <= 1024 } stated ? stated : null;

    private static Error Malformed(string member) =>
        Error.From(ErrorCodes.RequestMalformed, "member", JsonSerializer.SerializeToElement(member));

    private static Error Named(ErrorCode code, string member) =>
        Error.From(code, "member", JsonSerializer.SerializeToElement(member));

    private static TValue Withheld<TValue>(Error error, ref Error? failure)
    {
        failure = error;

        return default!;
    }

    // What the person entered beside the canonical form every rule is judged on, as an
    // invitation reads the identifiers an administrator names (REG-INV-001).
    private static Result<Entered> Read(IdentifierKind kind, string value, string member)
    {
        string entered = value?.Trim() ?? string.Empty;

        Entered? read = kind is IdentifierKind.Email
            ? EmailAddress.TryParse(entered, out EmailAddress address) ? new Entered(entered, address.Value) : null
            : PhoneNumber.TryParse(entered, out PhoneNumber number) ? new Entered(entered, number.Value) : null;

        if (read is null)
        {
            return Result.Failure<Entered>(Named(ErrorCodes.IdentifierInvalid, member));
        }

        if (!ScriptMixing.IsSingleScriptPerWord(read.Canonical))
        {
            return Result.Failure<Entered>(Named(ErrorCodes.IdentifierMixedScript, member));
        }

        return Result.Success(read);
    }

    // The person running bootstrap holds the server, which is what makes the
    // identifiers they name as good as verified (D-028).
    private static NewIdentifier Verified(IdentifierId id, IdentifierKind kind, Entered entered, DateTimeOffset at) =>
        new(id, kind, entered.Value, entered.Canonical, Locked: false, at);

    private static Entered Canary() =>
        EmailAddress.TryParse(CanaryAddress, out EmailAddress address)
            ? new Entered(CanaryAddress, address.Value)
            : throw new InvalidOperationException("The canary's address is not an address.");

    private static DisplayName CanaryDisplayName() =>
        DisplayName.TryParse(CanaryName, out DisplayName name)
            ? name
            : throw new InvalidOperationException("The canary's name is not a display name.");

    // Chapter 10 section 4.1a, the column "Administrative organization, at bootstrap".
    // The domain lock stays off, which is the system default, so it is not overridden.
    private static PolicyOverride Administrative(TimeSpan recency) =>
        new(
            AssuranceLevel.Aal2,
            new HashSet<Factor> { Factor.Passkey },
            Enum.GetValues<StepUpAction>().ToDictionary(
                action => action,
                _ => new Gate(GateLevel.Aal2, PhishingResistant: true, recency)),
            CredentialRedundancy.Enforced,
            SelfServiceRecovery: false,
            EmailDomains: null);

    // API-LAND-001: the link lands on the authentication application's own route. The
    // token travels in the fragment, which no request carries, so neither a server log
    // nor a referrer ever holds it.
    private static Uri Enrolment(Uri origin, OpaqueToken token) =>
        new(origin.GetLeftPart(UriPartial.Authority) + "/enrol#token=" + token.Value);

    // Bootstrap names no document, since none is published yet, and the subject holds no
    // other membership, so the limit on memberships has nothing to count.
    private async ValueTask<Error?> JoinAsync(
        SubjectId subject,
        OrganizationId organization,
        IReadOnlyList<RoleName> roles,
        DateTimeOffset at,
        CancellationToken cancellationToken) =>
        (await memberships
                .AttachAsync(subject, organization, [], roles, subject, Reason, multiple: false, at, cancellationToken)
                .ConfigureAwait(false))
            .Match<Error?>(_ => null, error => error);

    private sealed record Entered(string Value, string Canonical);
}
