using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
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
using Janus.Authorization.Gate;
using Janus.Authorization.Grants;
using Janus.Authorization.Groups;
using Janus.Authorization.Model;
using Janus.Authorization.Roles;
using Janus.Core;
using Janus.Core.Configuration;
using Janus.Hosting.Accounts;
using Janus.Hosting.Alerting;
using Janus.Hosting.Authentication;
using Janus.Hosting.Authorization;
using Janus.Hosting.Background;
using Janus.Hosting.Bff;
using Janus.Hosting.BreakGlass;
using Janus.Hosting.Configuration;
using Janus.Hosting.Credentials;
using Janus.Hosting.Events;
using Janus.Hosting.Maintenance;
using Janus.Hosting.Oidc;
using Janus.Hosting.Organizations;
using Janus.Hosting.Passwords;
using Janus.Hosting.Privacy;
using Janus.Hosting.Recovery;
using Janus.Hosting.Registration;
using Janus.Hosting.Sending;
using Janus.Hosting.Sessions;
using Janus.Privacy;
using Janus.Privacy.Breaches;
using Janus.Privacy.Consents;
using Janus.Privacy.Documents;
using Janus.Privacy.Erasures;
using Janus.Privacy.Exports;
using Janus.Privacy.Outbox;
using Janus.Privacy.Policies;
using Janus.Privacy.Records;
using Janus.Privacy.Requests;
using Janus.Privacy.Takedowns;
using Janus.Storage;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Janus.Hosting;

/// <summary>
/// The one method a host calls to register the library.
/// </summary>
/// <remarks>
/// Implements CONV-DESIGN-007, CONV-LAYOUT-002 and AUTHZ-SEAM-001. What the host
/// declares about its own domain is built and checked here, once, so a declaration that
/// does not hold together stops the deployment rather than the first request that reads
/// it (AUTHZ-MODEL-004).
/// </remarks>
public static class HostingRegistration
{
    /// <summary>
    /// Registers the library over the host's database and declared domain.
    /// </summary>
    /// <param name="services">The host's collection.</param>
    /// <param name="connectionString">
    /// The application's own credential, which holds row-level access and no schema
    /// right (OPS-MIG-003).
    /// </param>
    /// <param name="keyEncryptionKeys">
    /// The versions a subject key may be wrapped under, read from the secrets manager
    /// at startup and never from the database (OPS-SEC-001).
    /// </param>
    /// <param name="fingerprintKeys">
    /// The versions the searchable fingerprints are computed under, read from the same
    /// place and held outside the database (PRIV-RIGHT-005c).
    /// </param>
    /// <param name="signOnSecret">
    /// What this application presents at the provider's token endpoint when it
    /// establishes its own session, read from the same place and never from
    /// configuration (BFF-SESS-006, OPS-SEC-001).
    /// </param>
    /// <param name="maintenanceCredential">
    /// The database connection the scheduled maintenance runs under, which holds the
    /// maintenance role's rights and nothing else, read from the same place and never
    /// from configuration (OPS-MIG-003a, INF-HOST-003).
    /// </param>
    /// <param name="declaration">What the host declared about its own domain.</param>
    /// <param name="application">
    /// Which of the deployment's applications this process serves, which decides the
    /// one cookie attribute that differs between them (BFF-CSRF-005).
    /// </param>
    /// <returns>The collection, for chaining.</returns>
    /// <exception cref="ArgumentNullException">The collection is absent.</exception>
    /// <exception cref="StartupException">
    /// The key material is not there to be had, or the declaration does not hold
    /// together.
    /// </exception>
    public static IServiceCollection AddJanus(
        this IServiceCollection services,
        string connectionString,
        KeyEncryptionKeys keyEncryptionKeys,
        FingerprintKeys fingerprintKeys,
        ReadOnlyMemory<byte> signOnSecret,
        ReadOnlyMemory<byte> maintenanceCredential,
        AuthorizationDeclaration declaration,
        ApplicationKind application)
    {
        ArgumentNullException.ThrowIfNull(services);

        // AUTH-KEY-002 and OPS-SEC-001: both values come from the secrets manager and
        // the library holds no fallback for either, so a deployment that reached
        // neither stops here with the code that names why, not at the first request
        // that would have read a person's field.
        Present(keyEncryptionKeys, fingerprintKeys, signOnSecret, maintenanceCredential);

        // CONV-DESIGN-007: time is injected, and a host that has its own clock keeps it.
        services.TryAddSingleton(TimeProvider.System);

        services.AddStorageArea(connectionString, keyEncryptionKeys, fingerprintKeys);
        services.AddSingleton(AuthorizationModel.Of(declaration));

        // AUTHZ-GROUP-002: one set per operation, which is what makes ten checks in one
        // request resolve membership once.
        services.AddScoped<SubjectSets>();

        // LIB-HOST-004: the assurance provider is the host's to supply, and a host
        // that supplies none is one where nothing reports what a session has proved
        // other than the library's own session, which is judged where it carries the
        // request (AUTH-STEP-002).
        services.AddScoped<ISessionGates, RequestGates>();
        services.AddScoped(services => new StepUpGates(
            services.GetRequiredService<ISessionGates>(),
            services.GetService<IAssuranceProvider>()));

        // OPS-ALERT-006: an export is gated, limited and recorded inside the gate, so no
        // host path exercises one around it.
        services.AddScoped<ExportOperations>();

        // API-CONV-002: a body the reader could not parse is answered by the library
        // with a code and a correlation identifier, so the reader raises the failure
        // instead of writing a bare status the pipeline never sees.
        _ = services.Configure<RouteHandlerOptions>(options => options.ThrowOnBadRequest = true);

        // BFF-OWN-001: the browser boundary is the library's, so what issues a cookie
        // and what validates a token are registered here and not left to the host.
        services.AddSingleton(new BrowserSessionCookies(application));
        services.AddScoped<SynchronizerTokens>();
        services.AddScoped<Concealment>();
        services.AddScoped<MalformedRequest>();
        services.AddScoped<ResourceIsolation>();
        services.AddScoped<CustomRequestHeader>();
        services.AddScoped<OriginValidation>();
        services.AddScoped<RequestSession>();
        services.AddScoped<SessionResolution>();
        services.AddScoped<FirstContact>();
        services.AddScoped<SynchronizerToken>();
        services.AddScoped<SessionRequirement>();
        services.AddScoped<MachineProfile>();

        // BFF-LOG-002: whatever request logging the host turns on, a marked body
        // stays out of it.
        services.AddHttpLoggingInterceptor<SensitiveBodyLogging>();

        // BFF-SESS-006: the client half of the sign-on is the library's, so what it
        // presents, where it presents it and the connection it presents it on are
        // registered here and a host supplies none of them.
        services.AddSingleton(new SignOnSecret(signOnSecret));
        services.AddScoped<SignOn>();
        _ = services.AddHttpClient(SignOn.Channel);

        // LIB-HOST-001: what the host declares about its own messaging is the host's.
        // A deployment that declares none of it starts, and the checks that would have
        // read a declaration find nothing to read.
        services.TryAddSingleton(RestrictionKeySuppliers.None);

        // AUTH-ABUSE-004, OPS-ALERT-001: the one path every message takes, and what
        // decides whether it goes.
        services.AddScoped<SmsBalance>();
        services.AddScoped<RelayRegistration>();
        services.AddScoped<SendingService>();

        // LIB-EXT-001: the shipped handler carries email and SMS; a deployment that
        // registers its own before this runs keeps it.
        services.TryAddScoped<INotificationHandler>(
            provider => provider.GetRequiredService<SendingService>());

        // LIB-EXT-001: the shipped catalogue words every message in the languages the
        // library carries, and is likewise kept only where the deployment registered
        // none of its own. A deployment that registers neither still starts.
        services.TryAddSingleton<IMessageTemplates, DefaultMessageTemplates>();
        services.AddScoped(services => new PhoneSignals(
            services.GetService<PhoneSignalProvider>(),
            services.GetRequiredService<IPhoneSignalAudit>(),
            services.GetRequiredService<IUnitOfWork>(),
            services.GetRequiredService<TimeProvider>()));
        services.AddScoped(provider => new SendingValidation(
            provider.GetRequiredService<IConfigurationStore>(),
            provider.GetRequiredService<IMessageTemplates>(),
            provider.GetRequiredService<RestrictionKeySuppliers>()));
        services.AddScoped<ConfigurationAdministration>();
        services.AddScoped<RestrictionAdministration>();
        services.AddScoped<IRestrictionSet, RestrictionSetService>();
        services.AddScoped<ThrottleService>();
        services.AddScoped<NonExistenceNotice>();
        services.AddScoped<CallbackAdmission>();
        services.AddScoped<CallbackReferences>();
        services.AddScoped<ICallbackReferences>(
            provider => provider.GetRequiredService<CallbackReferences>());
        services.AddScoped<DeliveryReports>();

        // IDN-LIFE-012a: a provider's events are verified against the keys it publishes,
        // read on a client of the framework's factory and held between events; a
        // deployment that declares no provider takes none.
        services.AddSingleton<ProviderKeys>();
        _ = services.AddHttpClient(ProviderKeys.Channel);
        services.AddScoped<ProviderEvents>();
        services.AddScoped<ProviderEventIntake>();
        services.AddScoped(services => new BotDefence(
            services.GetRequiredService<IConfigurationStore>(),
            services.GetRequiredService<IDatacenterRanges>(),
            services.GetRequiredService<IRegistrationSources>(),
            services.GetRequiredService<IBotDefenceAudit>(),
            services.GetRequiredService<IUnitOfWork>(),
            services.GetService<ChallengeVerifier>(),
            services.GetRequiredService<TimeProvider>()));
        services.AddScoped<AlertRouter>();
        services.AddScoped<IAlertChannels, AlertChannels>();
        services.AddScoped<AlertDispatch>();

        // LIB-API-001, CONV-DESIGN-002: an emitted event is a row on the transaction
        // that made it true, offered to the host's consumers once that has committed.
        services.TryAddScoped<IEvents, EventOutbox>();
        services.AddScoped<EventConsumers>();
        services.AddScoped<EventPublisher>();

        // OPS-BOOT-002, OPS-BOOT-004: the sealed emergency credential, and OPS-BOOT-001
        // AC3: its absence raised until one is generated.
        services.AddScoped<BreakGlassService>();
        services.AddScoped<EmergencyCredentialWatch>();

        // OPS-MAINT-001: the licences and permits warned of, and the maintenance log.
        services.AddScoped<MaintenanceRecords>();
        services.AddScoped<LicenceExpiry>();

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

        // DR-007, DR-008: the backups and the throwaway instance are the environment's,
        // so what restores into one is the deployment's to register, and one it does not
        // register fails every test; what is restored is opened with the keys this
        // process holds, which is what the test proves the backup readable with.
        services.AddScoped(provider => new RestoreTest(
            provider.GetService<IRestoreTestInstance>(),
            keyEncryptionKeys,
            fingerprintKeys,
            provider.GetRequiredService<IConfigurationStore>(),
            provider.GetRequiredService<IPrivacyAudit>(),
            provider.GetRequiredService<IAlertChannels>(),
            provider.GetRequiredService<IUnitOfWork>(),
            provider.GetRequiredService<TimeProvider>(),
            provider.GetRequiredService<ILogger<RestoreTest>>()));

        // PRIV-RET-002, OPS-MIG-003a: the partitions are reached over the maintenance
        // credential only, in an area of their own built with the keys this process holds.
        services.AddSingleton(new MaintenanceCredential(maintenanceCredential));
        services.AddScoped(provider => new AuditRetention(
            provider.GetRequiredService<MaintenanceCredential>(),
            keyEncryptionKeys,
            fingerprintKeys,
            provider.GetRequiredService<IConfigurationStore>(),
            provider.GetRequiredService<IPrivacyAudit>(),
            provider.GetRequiredService<IUnitOfWork>(),
            provider.GetRequiredService<TimeProvider>()));
        services.AddScoped<AlertDestinationChange>();
        services.AddScoped<IAlertLog, AlertLog>();
        services.AddScoped<IConfigurationAdministration, ConfigurationService>();

        // AUTH-SESS-001, AUTH-PASS-004, AUTH-FACT-005: the authentication services,
        // each of which reads the settings table for what it enforces.
        services.AddScoped<PolicyResolution>();
        services.AddScoped<Janus.Authentication.Policies.AdministrativeScope>();
        services.AddSingleton<Argon2idHasher>();
        services.AddScoped<IScreeningLog, ScreeningLog>();
        services.AddSingleton<IWordList>(_ => new WordList(Corpus));

        // INT-PWD-001: the range API is reached over the framework's client, which
        // rotates its connections; the list the package carries answers when it
        // cannot (INT-PWD-002).
        services.AddHttpClient<ILeakedPasswordCorpus, LeakedPasswordCorpus>((requests, provider) =>
        {
            requests.BaseAddress = LeakedPasswordCorpus.Provider;

            return new LeakedPasswordCorpus(
                requests,
                provider.GetRequiredService<IConfigurationStore>(),
                provider.GetRequiredService<TimeProvider>(),
                new OfflineCorpus());
        });

        services.AddScoped<PasswordScreening>();
        services.AddScoped<PasswordService>();
        services.AddScoped<PreAuthenticationService>();

        // INT-GEN-006: one copy of the location file for the process, read from the
        // file the deployment supplies, where it supplies one.
        services.AddSingleton<LocationCopy>();
        services.AddScoped(provider => new LocationDatabase(
            provider.GetRequiredService<LocationCopy>(),
            provider.GetService<ILocationSource>(),
            provider.GetRequiredService<IConfigurationStore>(),
            provider.GetRequiredService<IAlertChannels>(),
            provider.GetRequiredService<TimeProvider>()));
        services.AddScoped<ILocationResolver>(provider => provider.GetRequiredService<LocationDatabase>());
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

        services.TryAddSingleton(PreferenceDeclarations.None);
        services.TryAddSingleton(ReservedUsernames.Default);

        // REG-PM-001, LIB-HOST-001: the frontend's pages are the host's to declare and
        // the library has no address to fall back on, so a deployment that registered
        // none is stopped at startup and none is registered here.
        services.AddScoped(provider => new DeclarationCoverage(
            provider.GetService<PasskeyAddresses>(),
            provider.GetService<AuthenticationAddresses>(),
            provider.GetService<SignOnClient>(),
            provider.GetService<IMailServer>(),
            provider.GetService<MailServerClient>(),
            provider.GetService<ImageCodec>(),
            provider.GetServices<SocialProvider>(),
            provider.GetRequiredService<IConfigurationStore>()));

        services.ConfigureHttpJsonOptions(ReadThroughContexts);
        services.AddScoped<RegistrationService>();
        services.AddScoped<IRegistration>(provider => provider.GetRequiredService<RegistrationService>());
        services.AddScoped<IdentifierService>();
        services.AddScoped<IIdentifiers>(provider => provider.GetRequiredService<IdentifierService>());
        services.AddScoped<AccountLifecycle>();

        // IDN-ATTR-002, LIB-HOST-001: the codec is the deployment's and may be absent,
        // so what needs it takes it as it was registered and refuses without it.
        services.AddScoped(provider => new ProfilePhotos(
            provider.GetRequiredService<IAccountDirectory>(),
            provider.GetRequiredService<Janus.Authentication.Policies.IMembershipLookup>(),
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
        services.AddScoped<ICredentials, CredentialService>();
        services.AddScoped<SigningKeys>();
        services.AddOidc(keyEncryptionKeys);
        services.AddScoped<OidcService>();
        services.AddScoped<IOidc>(provider => provider.GetRequiredService<OidcService>());

        // LIB-HOST-001, PRIV-RIGHT-005b: what the host declared is read back at
        // startup against the handlers it registered, so the declaration is here as
        // the host wrote it and not only as the model rebuilt it.
        services.AddSingleton(declaration);
        services.AddScoped<HandlerCoverage>();
        services.AddScoped<ConfigurationCoverage>();

        services.AddScoped<IPrivacyAlerts, PrivacyAlerts>();
        services.AddScoped<ILegalDocuments, LegalDocumentService>();
        services.AddScoped<Janus.Privacy.Policies.AdministrativeScope>();
        services.AddScoped<Supersession>();
        services.AddScoped<IConsents, ConsentService>();
        services.AddScoped<ISubjectNotices, SubjectNotices>();
        services.AddScoped<WorkingCalendar>();
        services.AddScoped<RestrictionGrant>();
        services.AddScoped<DeadlineSweep>();
        services.AddScoped<HolidayListWatch>();
        services.AddScoped<IPrivacyRequests, PrivacyRequestService>();
        services.AddScoped<ITakedowns, TakedownService>();

        // DR-016: the off-host ledger is the deployment's to register; one it does not
        // register leaves its erasures completing without a line, the residual R-A13
        // accepts until the tier upgrade.
        services.AddScoped<IErasures>(provider => new ErasureService(
            provider.GetRequiredService<Janus.Privacy.Policies.AdministrativeScope>(),
            provider.GetRequiredService<IStepUpGate>(),
            provider.GetRequiredService<IOutboxStore>(),
            provider.GetRequiredService<IErasureStore>(),
            provider.GetServices<ISubjectEventSubscriber>(),
            provider.GetService<IErasureLedger>(),
            provider.GetRequiredService<IPrivacyAudit>(),
            provider.GetRequiredService<IUnitOfWork>(),
            provider.GetRequiredService<TimeProvider>()));

        services.AddScoped<DeletionSweep>();
        services.AddScoped<OrganizationErasureSweep>();
        services.AddScoped<IExports, ExportService>();
        services.AddScoped<IProcessingRecords, ProcessingRecordsService>();
        services.AddScoped<IAuditTrail, AuditTrailService>();
        services.AddScoped(provider => new OutboxPublisher(
            provider.GetRequiredService<IOutboxStore>(),
            provider.GetRequiredService<IErasureStore>(),
            provider.GetServices<ISubjectEventSubscriber>(),
            provider.GetService<IErasureLedger>(),
            provider.GetRequiredService<IConfigurationStore>(),
            provider.GetRequiredService<IPrivacyAlerts>(),
            provider.GetRequiredService<IUnitOfWork>(),
            provider.GetRequiredService<TimeProvider>(),
            provider.GetRequiredService<RandomNumberGenerator>()));

        // AUTHZ-MODEL-001: what may be processed for what is part of the one
        // declaration the host makes, so the privacy side reads it from there rather
        // than asking the host a second time.
        services.AddSingleton(provider =>
            provider.GetRequiredService<AuthorizationModel>().Processing);

        services.AddScoped<Derivations>();
        services.AddScoped<ReverseLookup>();
        services.AddScoped<IAccessAlerts, AccessAlerts>();
        services.AddScoped<DenialSpikes>();

        // BFF-ERR-003: what the gate concealed is answered by stage 11 of the same
        // request, so the two share one holder.
        services.AddScoped<ConcealedRefusals>();
        services.AddScoped<IConcealedRefusals>(provider => provider.GetRequiredService<ConcealedRefusals>());
        services.AddScoped<IAccessGate, AccessGate>();

        // OPS-ALERT-005: the host says how many records a filtered query of its own
        // returned, and the library counts them against the person given them.
        services.AddScoped<ReadVolume>();
        services.AddScoped<IReadVolume>(provider => provider.GetRequiredService<ReadVolume>());
        services.AddScoped<Janus.Authorization.Gate.AdministrativeScope>();
        services.AddScoped<IGrants, GrantService>();
        services.AddScoped<IRoles, RoleService>();
        services.AddScoped<IOrganizations, OrganizationService>();

        // REG-DOM-001, LIB-EXT-001: the resolver is the deployment's and may be absent,
        // so what reads a record takes it as it was registered and proves nothing
        // without it.
        services.AddScoped<DomainLock>();
        services.AddScoped<IOrganizationDomains>(provider => new OrganizationDomainService(
            provider.GetRequiredService<Janus.Authentication.Policies.AdministrativeScope>(),
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
            provider.GetService<IMailServer>(),
            provider.GetRequiredService<IConfigurationStore>(),
            provider.GetRequiredService<IAlertChannels>(),
            provider.GetRequiredService<IUnitOfWork>(),
            provider.GetRequiredService<TimeProvider>(),
            provider.GetRequiredService<RandomNumberGenerator>()));
        services.AddScoped(provider => new MailboxReconciliation(
            provider.GetRequiredService<IMailboxStore>(),
            provider.GetService<IMailServer>(),
            provider.GetRequiredService<IAlertChannels>(),
            provider.GetRequiredService<TimeProvider>()));

        // INT-MAIL-010: the app passwords are the mail server's, reached with a token
        // the provider issues to the server's client; without a server there are none.
        services.AddScoped<IMailServerTokens>(provider => new MailServerTokens(
            provider.GetRequiredService<OpenIddict.Server.IOpenIddictServerFactory>(),
            provider.GetRequiredService<OpenIddict.Server.IOpenIddictServerDispatcher>(),
            provider.GetRequiredService<OidcService>(),
            provider.GetRequiredService<IOidcClientStore>(),
            provider.GetService<MailServerClient>(),
            provider.GetRequiredService<AuthenticationAddresses>(),
            provider.GetRequiredService<TimeProvider>()));
        services.AddScoped<IAppPasswords>(provider => new AppPasswords(
            provider.GetService<IMailServer>(),
            provider.GetRequiredService<IMailServerTokens>(),
            provider.GetRequiredService<IMailboxStore>(),
            provider.GetRequiredService<IAccountDirectory>(),
            provider.GetRequiredService<StepUpGuard>(),
            provider.GetRequiredService<IIdentifierDirectory>(),
            provider.GetRequiredService<INotificationHandler>(),
            provider.GetRequiredService<IConfigurationStore>(),
            provider.GetRequiredService<ICredentialAudit>(),
            provider.GetRequiredService<IUnitOfWork>(),
            provider.GetRequiredService<TimeProvider>()));

        services.AddScoped<InvitationAcknowledgement>();
        services.AddScoped<MembershipEnd>();

        // REG-MAIL-001: an invitation reserves a mailbox only where there is a mail
        // server to create it on.
        services.AddScoped(provider => new InvitationService(
            provider.GetRequiredService<IAccessGate>(),
            provider.GetRequiredService<Janus.Authentication.Policies.AdministrativeScope>(),
            provider.GetRequiredService<StepUpGuard>(),
            provider.GetRequiredService<IOrganizationDirectory>(),
            provider.GetRequiredService<IRoleCatalogue>(),
            provider.GetRequiredService<ILegalDocuments>(),
            provider.GetRequiredService<DomainLock>(),
            provider.GetRequiredService<IInvitationStore>(),
            provider.GetRequiredService<IAccountDirectory>(),
            provider.GetRequiredService<InvitationAcknowledgement>(),
            provider.GetRequiredService<MembershipEnd>(),
            provider.GetRequiredService<IMailboxStore>(),
            provider.GetService<IMailServer>(),
            provider.GetRequiredService<INotificationHandler>(),
            provider.GetRequiredService<IConfigurationStore>(),
            provider.GetRequiredService<IOrganizationAudit>(),
            provider.GetRequiredService<IUnitOfWork>(),
            provider.GetRequiredService<TimeProvider>(),
            provider.GetRequiredService<RandomNumberGenerator>()));
        services.AddScoped<IInvitations>(provider => provider.GetRequiredService<InvitationService>());
        services.AddScoped<IGroups, GroupService>();
        services.AddScoped<IDerivationMaterialiser, DerivationMaterialiser>();
        services.AddScoped<ModelValidation>();
        services.AddScoped<RedirectValidation>();
        services.AddScoped<SchemaValidation>();

        // AUTHZ-MODEL-004 AC2 (D-160): what a hosted service starts before is what was
        // registered after it, and the web server is one, so the checks that read the
        // database go at the head of the collection. OPS-MIG-002 leads them, because
        // every one of the others reads a table, and LIB-HOST-001 follows, because
        // most of them read a key the deployment has to name.
        services.Insert(0, ServiceDescriptor.Singleton<IHostedService, SchemaValidationService>());
        services.Insert(1, ServiceDescriptor.Singleton<IHostedService, SettingsValidationService>());
        services.Insert(2, ServiceDescriptor.Singleton<IHostedService, ModelValidationService>());
        services.Insert(3, ServiceDescriptor.Singleton<IHostedService, SendingValidationService>());
        services.Insert(4, ServiceDescriptor.Singleton<IHostedService, HandlerValidationService>());
        services.Insert(5, ServiceDescriptor.Singleton<IHostedService, ConfigurationValidationService>());
        services.Insert(6, ServiceDescriptor.Singleton<IHostedService, DeclarationValidationService>());
        services.Insert(7, ServiceDescriptor.Singleton<IHostedService, RedirectValidationService>());
        services.Insert(8, ServiceDescriptor.Singleton<IHostedService, SigningKeyValidationService>());
        services.Insert(9, ServiceDescriptor.Singleton<IHostedService, RelayValidationService>());

        // INF-BG-001: the scheduled work starts once the checks above have passed.
        services.AddHostedService(provider => new BackgroundWorker(
            provider.GetRequiredService<IServiceScopeFactory>(),
            BackgroundJobs.All,
            provider.GetRequiredService<TimeProvider>(),
            provider.GetRequiredService<ILogger<BackgroundWorker>>()));

        return services;
    }

    /// <summary>
    /// Reads every request body through the generated contexts and through nothing
    /// else.
    /// </summary>
    /// <param name="options">The options minimal APIs read a request with.</param>
    /// <remarks>
    /// Implements CONV-DESIGN-006 and CONV-CODE-004. The chain the framework starts
    /// with holds the reflection resolver, and a context added after it is never asked,
    /// so the chain is replaced rather than added to: a body no context declares fails
    /// where it is first read instead of being reflected over.
    /// </remarks>
    /// <exception cref="ArgumentNullException">The options are absent.</exception>
    internal static void ReadThroughContexts(JsonOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        // The contexts spell an enum as the contract spells it, and a request is read
        // through these options rather than through a context's own, so the same
        // converter stands here (API-CONV-002).
        options.SerializerOptions.Converters.Add(new JsonStringEnumConverter<IdentifierKind>());
        options.SerializerOptions.Converters.Add(new JsonStringEnumConverter<Factor>());
        options.SerializerOptions.Converters.Add(new JsonStringEnumConverter<TakedownTrigger>());
        options.SerializerOptions.Converters.Add(new JsonStringEnumConverter<SubjectType>());
        options.SerializerOptions.TypeInfoResolverChain.Clear();
        options.SerializerOptions.TypeInfoResolverChain.Add(RegistrationJson.Default);
        options.SerializerOptions.TypeInfoResolverChain.Add(AuthenticationJson.Default);
        options.SerializerOptions.TypeInfoResolverChain.Add(BreakGlassJson.Default);
        options.SerializerOptions.TypeInfoResolverChain.Add(MaintenanceJson.Default);
        options.SerializerOptions.TypeInfoResolverChain.Add(AccountJson.Default);
        options.SerializerOptions.TypeInfoResolverChain.Add(RecoveryJson.Default);
        options.SerializerOptions.TypeInfoResolverChain.Add(CredentialsJson.Default);
        options.SerializerOptions.TypeInfoResolverChain.Add(WellKnownJson.Default);
        options.SerializerOptions.TypeInfoResolverChain.Add(PrivacyJson.Default);
        options.SerializerOptions.TypeInfoResolverChain.Add(ConfigurationJson.Default);
        options.SerializerOptions.TypeInfoResolverChain.Add(SendingJson.Default);
        options.SerializerOptions.TypeInfoResolverChain.Add(AuthorizationJson.Default);
        options.SerializerOptions.TypeInfoResolverChain.Add(OrganizationJson.Default);
    }

    // The word list is a file a deployment holds beside the application, where it
    // rejects on one (AUTH-PASS-004).
    private static string Corpus =>
        Path.Combine(AppContext.BaseDirectory, WordList.Directory);

    // The fingerprint key computes an HMAC-SHA256, so a version shorter than that hash
    // is a key that weakens the code it is used by and is not a key the library runs on.
    private static void Present(
        KeyEncryptionKeys keyEncryptionKeys,
        FingerprintKeys fingerprintKeys,
        ReadOnlyMemory<byte> signOnSecret,
        ReadOnlyMemory<byte> maintenanceCredential)
    {
        if (keyEncryptionKeys is null)
        {
            throw new StartupException(
                "The key-encryption key was not supplied; the library reads it from the secrets manager and holds no fallback.",
                Error.From(ErrorCodes.StartupKeyUnavailable, "key", JsonSerializer.SerializeToElement("keyEncryptionKeys")));
        }

        if (fingerprintKeys is null
            || fingerprintKeys.Versions.Values.Any(version => version.Length < FingerprintKeys.MinimumLength))
        {
            throw new StartupException(
                "The fingerprint key was not supplied, or a version of it is shorter than the hash it computes.",
                Error.From(ErrorCodes.StartupKeyUnavailable, "key", JsonSerializer.SerializeToElement("fingerprintKeys")));
        }

        // BFF-SESS-006: an application that cannot authenticate itself at the token
        // endpoint cannot establish a session at all, so it stops here rather than at
        // the first person who arrives holding nothing.
        if (signOnSecret.Length is 0)
        {
            throw new StartupException(
                "The sign-on client secret was not supplied; the library reads it from the secrets manager and holds no fallback.",
                Error.From(ErrorCodes.StartupKeyUnavailable, "key", JsonSerializer.SerializeToElement("signOnSecret")));
        }

        // PRIV-RET-002: without the maintenance credential no month is created ahead and
        // no expired one is dropped, so the trail stops taking rows once the months the
        // migration created have passed; the deployment stops here instead.
        if (maintenanceCredential.Length is 0)
        {
            throw new StartupException(
                "The maintenance credential was not supplied; the library reads it from the secrets manager and holds no fallback.",
                Error.From(ErrorCodes.StartupKeyUnavailable, "key", JsonSerializer.SerializeToElement("maintenanceCredential")));
        }
    }

}
