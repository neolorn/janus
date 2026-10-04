using System;
using System.Security.Cryptography;
using Janus.Authentication.Accounts;
using Janus.Authentication.Alerting;
using Janus.Authentication.BreakGlass;
using Janus.Authentication.Callbacks;
using Janus.Authentication.Configuration;
using Janus.Authentication.Credentials;
using Janus.Authentication.Events;
using Janus.Authentication.Factors;
using Janus.Authentication.Identifiers;
using Janus.Authentication.Invitations;
using Janus.Authentication.Mailboxes;
using Janus.Authentication.Maintenance;
using Janus.Authentication.Oidc;
using Janus.Authentication.Organizations;
using Janus.Authentication.Passwords;
using Janus.Authentication.Policies;
using Janus.Authentication.Recovery;
using Janus.Authentication.Registration;
using Janus.Authentication.Sending;
using Janus.Authentication.Sessions;
using Janus.Authentication.SignIn;
using Janus.Core;
using Janus.Core.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Janus.Authentication;

/// <summary>
/// The one method that registers everything this project provides.
/// </summary>
/// <remarks>Implements CONV-DESIGN-007.</remarks>
internal static class AuthenticationRegistration
{
    /// <summary>
    /// Registers the services of the account, identifier, registration, organization,
    /// invitation, session, factor, credential and recovery operations, the governed
    /// send path and its restrictions, the event outbox, the alert channels, the
    /// provider's credential source, and the watches and sweeps the worker runs.
    /// </summary>
    /// <param name="services">The host's collection.</param>
    /// <returns>The collection, for chaining.</returns>
    /// <exception cref="ArgumentNullException">The collection is absent.</exception>
    /// <remarks>
    /// A declaration the host may leave out reaches the type that uses it through a
    /// factory that asks the container for it (D-180).
    /// </remarks>
    public static IServiceCollection AddAuthenticationArea(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // LIB-API-001, CONV-DESIGN-002: an emitted event is a row on the transaction
        // that made it true, offered to the host's consumers once that has committed.
        // Publication is not a default a host replaces (LIB-EXT-001, D-166, 320), so an
        // IEvents registered before this one does not pre-empt it; a host consumes an
        // event through IEventConsumer<TEvent>.
        services.RemoveAll<IEvents>();
        services.AddScoped<IEvents, EventOutbox>();
        services.AddScoped<IAlertChannels, AlertChannels>();

        // AUTH-ABUSE-004, OPS-ALERT-001: what decides whether a message goes.
        services.AddScoped<SmsBalance>();
        services.AddScoped<RelayRegistration>();
        services.AddScoped(provider => new PhoneSignals(
            provider.GetService<PhoneSignalProvider>(),
            provider.GetRequiredService<IPhoneSignalAudit>(),
            provider.GetRequiredService<IUnitOfWork>(),
            provider.GetRequiredService<TimeProvider>()));
        services.AddScoped<ConfigurationAdministration>();
        services.AddScoped<RestrictionAdministration>();
        services.AddScoped<SendCounterSweep>();

        // API-LAND-001: every link lands on an origin the host declared, and one it did
        // not declare stops the start, so nothing is registered in its place.
        services.AddScoped(provider => new LandingLinks(provider.GetRequiredService<LandingOrigins>()));
        services.AddScoped<IRestrictionSet, RestrictionSetService>();
        services.AddScoped<ThrottleService>();
        services.AddScoped<NonExistenceNotice>();
        services.AddScoped<CallbackAdmission>();
        services.AddScoped<CallbackReferences>();
        services.AddScoped<ICallbackReferences>(
            provider => provider.GetRequiredService<CallbackReferences>());
        services.AddScoped<DeliveryReports>();

        // IDN-LIFE-012a, IDN-LIFE-012, REG-IDENT-008: what a provider's verified events
        // do, and the attempts this application makes as the providers' client.
        services.AddScoped<ProviderEvents>();
        services.AddScoped<ProviderAttempts>();
        services.AddScoped(provider => new BotDefence(
            provider.GetRequiredService<IConfigurationStore>(),
            provider.GetRequiredService<IDatacenterRanges>(),
            provider.GetRequiredService<IRegistrationSources>(),
            provider.GetRequiredService<IBotDefenceAudit>(),
            provider.GetRequiredService<IUnitOfWork>(),
            provider.GetService<ChallengeVerifier>(),
            provider.GetRequiredService<TimeProvider>()));

        // OPS-BOOT-002, OPS-BOOT-004: the sealed emergency credential, and OPS-BOOT-001
        // AC3: its absence raised until one is generated.
        services.AddScoped<BreakGlassService>();
        services.AddScoped<IBreakGlass>(provider => provider.GetRequiredService<BreakGlassService>());
        services.AddScoped<EmergencyCredentialWatch>();

        // OPS-MAINT-001: the licences and permits warned of, and the maintenance log.
        // DR-009a: the annual operation the key-encryption key is rotated in, warned of
        // from the log.
        services.AddScoped<IMaintenanceRecords, MaintenanceRecords>();
        services.AddScoped<LicenceExpiry>();
        services.AddScoped<EnvelopeRotationWatch>();

        // INF-HOST-001, INF-TLS-003: the clock and the renewer are the environment's, so
        // what measures them is the deployment's to register, and one it does not
        // register is raised as unwatched.
        services.AddScoped(provider => new ClockDriftWatch(
            provider.GetService<IClockReference>(),
            provider.GetRequiredService<IConfigurationStore>(),
            provider.GetRequiredService<IAlertChannels>(),
            provider.GetRequiredService<TimeProvider>()));
        services.AddScoped(provider => new CertificateRenewalWatch(
            provider.GetService<ICertificateRenewal>(),
            provider.GetRequiredService<IAlertChannels>(),
            provider.GetRequiredService<TimeProvider>()));

        // AUTH-SESS-001, AUTH-PASS-004, AUTH-FACT-005: the authentication services,
        // each of which reads the settings table for what it enforces.
        services.AddScoped<PolicyResolution>();
        services.AddScoped<AdministrativeScope>();
        services.AddSingleton<Argon2idHasher>();
        services.AddScoped<PasswordScreening>();
        services.AddScoped<PasswordService>();
        services.AddScoped<PreAuthenticationService>();
        services.AddScoped<SynchronizerTokens>();
        services.AddScoped<ConcurrentSessions>();
        services.AddScoped<SessionService>();
        services.AddScoped<ISessions>(provider => provider.GetRequiredService<SessionService>());
        services.AddScoped<TotpService>();
        services.AddScoped<WebAuthnService>();
        services.AddScoped<RecoveryCodeService>();
        services.AddScoped<RecoveryCodeReminders>();
        services.AddScoped<DeviceService>();
        services.AddScoped<StepUpGuard>();
        services.AddScoped<IStepUpGate, StepUpGate>();

        services.AddScoped<RegistrationService>();
        services.AddScoped<IRegistration>(provider => provider.GetRequiredService<RegistrationService>());
        services.AddScoped<IdentifierService>();
        services.AddScoped<IIdentifiers>(provider => provider.GetRequiredService<IdentifierService>());
        services.AddScoped<AccountLifecycle>();

        // IDN-ATTR-002, LIB-HOST-001: the codec is the deployment's and may be absent,
        // so what needs it takes it as it was registered and refuses without it.
        services.AddScoped(provider => new ProfilePhotos(
            provider.GetRequiredService<IAccountDirectory>(),
            provider.GetRequiredService<ISettingsRestriction>(),
            provider.GetRequiredService<IMembershipLookup>(),
            provider.GetRequiredService<IConfigurationStore>(),
            provider.GetRequiredService<IAccountAudit>(),
            provider.GetRequiredService<IUnitOfWork>(),
            provider.GetService<ImageCodec>(),
            provider.GetRequiredService<TimeProvider>()));
        services.AddScoped<AccountService>();
        services.AddScoped<IAccount>(provider => provider.GetRequiredService<AccountService>());
        services.AddScoped<IAccounts, AccountAdministration>();
        services.AddScoped<SignInLinks>();
        services.AddScoped<VerificationCodes>();
        services.AddScoped<AuthenticationService>();
        services.AddScoped<IAuthentication>(provider =>
            provider.GetRequiredService<AuthenticationService>());
        services.AddScoped<LossReports>();
        services.AddScoped<EnrolmentSessions>();
        services.AddScoped<RecoveryService>();
        services.AddScoped<IRecovery>(provider => provider.GetRequiredService<RecoveryService>());
        services.AddScoped<CredentialService>();
        services.AddScoped<ICredentials>(provider => provider.GetRequiredService<CredentialService>());

        // AUTH-KEY-001, CONV-DESIGN-007: the signing keys are one set for the life of
        // the process, which writes each change in a scope of its own.
        services.AddSingleton(provider => new SigningCredentialSource(
            provider.GetRequiredService<IServiceScopeFactory>(),
            provider.GetRequiredService<TimeProvider>()));
        services.AddScoped<SigningKeys>();
        services.AddScoped<RegisteredSecrets>();
        services.AddScoped<RedirectValidation>();
        services.AddScoped<OidcService>();
        services.AddScoped<IOidc>(provider => provider.GetRequiredService<OidcService>());

        services.AddScoped<IOrganizations, OrganizationService>();

        // REG-DOM-001, LIB-EXT-001: the resolver is the deployment's and may be absent,
        // so what reads a record takes it as it was registered and proves nothing
        // without it.
        services.AddScoped<DomainLock>();
        services.AddScoped<IOrganizationDomains>(provider => new OrganizationDomainService(
            provider.GetRequiredService<AdministrativeScope>(),
            provider.GetRequiredService<StepUpGuard>(),
            provider.GetRequiredService<IOrganizationDirectory>(),
            provider.GetRequiredService<IDomainStore>(),
            provider.GetRequiredService<IConfigurationStore>(),
            provider.GetRequiredService<ConfigurationAdministration>(),
            provider.GetService<IDnsResolver>(),
            provider.GetRequiredService<IOrganizationAudit>(),
            provider.GetRequiredService<IAlertChannels>(),
            provider.GetRequiredService<IUnitOfWork>(),
            provider.GetRequiredService<TimeProvider>(),
            provider.GetRequiredService<RandomNumberGenerator>()));
        services.AddScoped(provider => new DomainReverification(
            provider.GetRequiredService<IDomainStore>(),
            provider.GetService<IDnsResolver>(),
            provider.GetRequiredService<IConfigurationStore>(),
            provider.GetRequiredService<IAlertChannels>(),
            provider.GetRequiredService<IUnitOfWork>(),
            provider.GetRequiredService<TimeProvider>()));

        // INT-MAIL-006, INT-MAIL-008: the mail server is optional, and a deployment
        // that registers none provisions nothing and reconciles nothing.
        services.AddScoped(provider => new MailboxPublisher(
            provider.GetRequiredService<IMailboxStore>(),
            provider.GetRequiredService<IMailServerInUse>(),
            provider.GetRequiredService<IConfigurationStore>(),
            provider.GetRequiredService<IAlertChannels>(),
            provider.GetRequiredService<IUnitOfWork>(),
            provider.GetRequiredService<TimeProvider>(),
            provider.GetRequiredService<RandomNumberGenerator>()));
        services.AddScoped(provider => new MailboxReconciliation(
            provider.GetRequiredService<IMailboxStore>(),
            provider.GetRequiredService<IMailServerInUse>(),
            provider.GetRequiredService<IAlertChannels>(),
            provider.GetRequiredService<TimeProvider>()));

        // INT-MAIL-010: the app passwords are the mail server's, reached with a token
        // the provider issues to the server's client; without a server there are none.
        services.AddScoped<IAppPasswords>(provider => new AppPasswords(
            provider.GetRequiredService<IMailServerInUse>(),
            provider.GetRequiredService<IMailServerTokens>(),
            provider.GetRequiredService<IMailboxStore>(),
            provider.GetRequiredService<IAccountDirectory>(),
            provider.GetRequiredService<ISettingsRestriction>(),
            provider.GetRequiredService<StepUpGuard>(),
            provider.GetRequiredService<IIdentifierDirectory>(),
            provider.GetRequiredService<INotificationHandler>(),
            provider.GetRequiredService<IConfigurationStore>(),
            provider.GetRequiredService<ICredentialAudit>(),
            provider.GetRequiredService<IUnitOfWork>(),
            provider.GetRequiredService<TimeProvider>()));

        services.AddScoped<InvitationAcknowledgement>();
        services.AddScoped<MembershipEnd>();
        services.AddScoped<InvitationOpening>();

        // REG-MAIL-001: an invitation reserves a mailbox only where there is a mail
        // server to create it on.
        services.AddScoped(provider => new InvitationService(
            provider.GetRequiredService<IAccessGate>(),
            provider.GetRequiredService<AdministrativeScope>(),
            provider.GetRequiredService<StepUpGuard>(),
            provider.GetRequiredService<IOrganizationDirectory>(),
            provider.GetRequiredService<IRoleCatalogue>(),
            provider.GetRequiredService<ILegalDocuments>(),
            provider.GetRequiredService<DomainLock>(),
            provider.GetRequiredService<IInvitationStore>(),
            provider.GetRequiredService<IAccountDirectory>(),
            provider.GetRequiredService<IIdentifierDirectory>(),
            provider.GetRequiredService<InvitationAcknowledgement>(),
            provider.GetRequiredService<MembershipEnd>(),
            provider.GetRequiredService<IMailboxStore>(),
            provider.GetRequiredService<IMailServerInUse>(),
            provider.GetRequiredService<INotificationHandler>(),
            provider.GetRequiredService<LandingLinks>(),
            provider.GetRequiredService<IConfigurationStore>(),
            provider.GetRequiredService<IOrganizationAudit>(),
            provider.GetRequiredService<IUnitOfWork>(),
            provider.GetRequiredService<TimeProvider>(),
            provider.GetRequiredService<RandomNumberGenerator>()));
        services.AddScoped<IInvitations>(provider => provider.GetRequiredService<InvitationService>());

        return services;
    }
}
