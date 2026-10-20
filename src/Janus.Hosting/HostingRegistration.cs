using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using Janus.Authentication;
using Janus.Authentication.Accounts;
using Janus.Authentication.Alerting;
using Janus.Authentication.Configuration;
using Janus.Authentication.Factors;
using Janus.Authentication.Mailboxes;
using Janus.Authentication.Oidc;
using Janus.Authentication.Passwords;
using Janus.Authentication.Sending;
using Janus.Authentication.Sessions;
using Janus.Authorization;
using Janus.Authorization.Gate;
using Janus.Authorization.Model;
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
using Janus.Hosting.Mailboxes;
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
using Janus.Privacy.Documents;
using Janus.Privacy.Requests;
using Janus.Storage;
using Microsoft.AspNetCore.Http;
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
    /// <param name="declaration">What the host declared about its own domain.</param>
    /// <param name="application">
    /// Which of the deployment's applications this process serves, which decides the
    /// one cookie attribute that differs between them (BFF-CSRF-005).
    /// </param>
    /// <returns>The collection, for chaining.</returns>
    /// <exception cref="ArgumentNullException">The collection is absent.</exception>
    /// <exception cref="StartupException">The declaration does not hold together.</exception>
    /// <remarks>
    /// No secret is an argument here: every one the deployment needs is read through the
    /// host's secret source into the key ring when the application starts, before the
    /// server serves a request (CONV-DESIGN-007, CONV-CODE-007, OPS-SEC-001).
    /// </remarks>
    public static IServiceCollection AddJanus(
        this IServiceCollection services,
        string connectionString,
        AuthorizationDeclaration declaration,
        ApplicationKind application)
    {
        ArgumentNullException.ThrowIfNull(services);

        // CONV-DESIGN-007: time is injected, and a host that has its own clock keeps it.
        services.TryAddSingleton(TimeProvider.System);

        // CONV-DESIGN-007: each project registers the types it defines, and what follows
        // is this project's own.
        services.AddCoreArea();
        services.AddStorageArea(connectionString);
        services.AddAuthenticationArea();
        services.AddAuthorizationArea(declaration);
        services.AddPrivacyArea();

        // AUTH-STEP-002: the library's own session is judged where it carries the
        // request.
        services.AddScoped<ISessionGates, RequestGates>();

        // API-CONV-002: a body the reader could not parse is answered by the library
        // with a code and a correlation identifier, so the reader raises the failure
        // instead of writing a bare status the pipeline never sees.
        _ = services.Configure<RouteHandlerOptions>(options => options.ThrowOnBadRequest = true);

        // BFF-LOG-002 (D-197): the server's own entries for a request write its query
        // string at Information, so their category is held to Warning, which still has
        // the server open the scope that carries the request identifier (CONV-LOG-002).
        _ = services.AddLogging(logging => logging.AddFilter(
            RequestLoggingValidationService.Category,
            LogLevel.Warning));

        // BFF-OWN-001: the browser boundary is the library's, so what issues a cookie
        // and what validates a token are registered here and not left to the host.
        services.AddSingleton(new BrowserSessionCookies(application));
        services.AddScoped<Concealment>();
        services.AddScoped<ErrorTranslation>();
        services.AddScoped<MalformedRequest>();
        services.AddScoped<ResourceIsolation>();
        services.AddScoped<CustomRequestHeader>();
        services.AddScoped<OriginValidation>();
        services.AddSingleton<SourceAdmissions>();
        services.AddScoped<SourceRateLimiting>();
        services.AddScoped<RequestSession>();

        // BFF-HOST-001: a host's own endpoint reads what stage 5 established for the
        // request, the one record the library's own endpoints read.
        services.AddScoped<IRequestAccess>(provider => provider.GetRequiredService<RequestSession>());
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
        // registered here and a host supplies none of them; what it presents is the
        // secret the registry holds for it (OPS-SEC-002).
        services.AddScoped<SignOn>();
        _ = services.AddHttpClient(SignOn.Channel);

        // LIB-TEST-001, D-172: the same half makes the conformance suite's provider
        // probes, as the same client and on the same connection.
        services.AddScoped<IProviderProbes, ProviderProbes>();

        // INT-MAIL-009: outbound delivery is registered apart from mailbox hosting.
        services.AddOutboundDelivery();

        // IDN-LIFE-012a: a provider's events are verified against the keys it publishes,
        // read on a client of the framework's factory and held between events; a
        // deployment that declares no provider takes none.
        services.AddSingleton<ProviderKeys>();
        _ = services.AddHttpClient(ProviderKeys.Channel);
        services.AddScoped<ProviderEventIntake>();

        // IDN-LIFE-012, REG-IDENT-008: this application is the providers' client, on
        // the same connection their documents are read on.
        services.AddScoped<ProviderSignIn>();
        services.AddScoped<AlertRouter>();
        services.AddScoped<AlertDispatch>();

        // LIB-API-001, CONV-DESIGN-002: a committed event is offered to the consumers
        // the host registered.
        services.AddScoped<EventConsumers>();
        services.AddScoped<EventPublisher>();

        // DR-007, DR-008: the backups and the throwaway instance are the environment's,
        // so what restores into one is the deployment's to register, and one it does not
        // register fails every test; what is restored is opened with the keys this
        // process holds, which is what the test proves the backup readable with.
        services.AddScoped(provider => new RestoreTest(
            provider.GetService<IRestoreTestInstance>(),
            provider.GetRequiredService<IKeyRing>(),
            provider.GetRequiredService<IConfigurationStore>(),
            provider.GetRequiredService<IPrivacyAudit>(),
            provider.GetRequiredService<IAlertChannels>(),
            provider.GetRequiredService<IUnitOfWork>(),
            provider.GetRequiredService<TimeProvider>(),
            provider.GetRequiredService<ILogger<RestoreTest>>()));

        // PRIV-RET-002, OPS-MIG-003a: the partitions are reached over the maintenance
        // credential only, in an area of their own built with the keys this process holds.
        services.AddScoped(provider => new AuditRetention(
            provider.GetRequiredService<IKeyRing>(),
            provider.GetRequiredService<IConfigurationStore>(),
            provider.GetRequiredService<IPrivacyAudit>(),
            provider.GetRequiredService<IUnitOfWork>(),
            provider.GetRequiredService<TimeProvider>()));
        services.AddScoped<AlertDestinationChange>();
        services.AddScoped<IAlertLog, AlertLog>();

        // IDN-ATTR-002, OPS-CFG-003: a system policy that shows photos is refused where
        // the deployment declared no codec, so what changes it takes the codec as it was
        // registered.
        services.AddScoped<IConfigurationAdministration>(provider => new ConfigurationService(
            provider.GetRequiredService<Janus.Authentication.Policies.AdministrativeScope>(),
            provider.GetRequiredService<StepUpGuard>(),
            provider.GetRequiredService<IConfigurationStore>(),
            provider.GetRequiredService<ConfigurationAdministration>(),
            provider.GetRequiredService<AlertDestinationChange>(),
            provider.GetRequiredService<AuthorizationDeclaration>(),
            provider.GetRequiredService<CategoryRetention>(),
            provider.GetService<ImageCodec>(),
            provider.GetRequiredService<NoticeCookieCheck>(),
            provider.GetRequiredService<IUnitOfWork>()));

        // AUTH-PASS-004: what a password is screened against, and the record of each
        // screening.
        services.AddScoped<IScreeningLog, ScreeningLog>();
        services.AddSingleton<IWordList>(provider => new WordList(provider.GetRequiredService<DictionaryWords>()));

        // INT-PWD-001: the range API is reached over the framework's client, which
        // rotates its connections, and every request it makes names the library; the
        // list the package carries answers when it cannot (INT-PWD-002).
        services.AddHttpClient<ILeakedPasswordCorpus, LeakedPasswordCorpus>((requests, provider) =>
        {
            requests.BaseAddress = LeakedPasswordCorpus.Provider;
            requests.DefaultRequestHeaders.UserAgent.Add(LeakedPasswordCorpus.Agent);

            return new LeakedPasswordCorpus(
                requests,
                provider.GetRequiredService<IConfigurationStore>(),
                provider.GetRequiredService<TimeProvider>(),
                new OfflineCorpus());
        });

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

        // AUTH-ABUSE-008: one copy of the datacenter range file for the process, read
        // from the file the deployment supplies, where it supplies one.
        services.AddSingleton<DatacenterRangeCopy>();
        services.AddScoped(provider => new DatacenterRanges(
            provider.GetRequiredService<DatacenterRangeCopy>(),
            provider.GetService<IDatacenterRangeSource>(),
            provider.GetRequiredService<IConfigurationStore>(),
            provider.GetRequiredService<IAlertChannels>(),
            provider.GetRequiredService<TimeProvider>()));
        services.AddScoped<IDatacenterRanges>(provider => provider.GetRequiredService<DatacenterRanges>());

        // REG-PM-001, LIB-HOST-001: the frontend's pages are the host's to declare and
        // the library has no address to fall back on, so a deployment that registered
        // none is stopped at startup and none is registered here.
        services.AddScoped(provider => new DeclarationCoverage(
            provider.GetService<PasskeyAddresses>(),
            provider.GetService<AuthenticationAddresses>(),
            provider.GetService<LandingOrigins>(),
            provider.GetService<SignOnClient>(),
            provider.GetRequiredService<IMailServerInUse>(),
            provider.GetService<MailServerClient>(),
            provider.GetService<ImageCodec>(),
            provider.GetService<IDnsResolver>(),
            provider.GetServices<SocialProvider>(),
            provider.GetServices<RelationshipSource>(),
            provider.GetRequiredService<AuthorizationDeclaration>(),
            provider.GetServices<ISubjectEventSubscriber>(),
            provider.GetServices<IPurposeHandler>(),
            provider.GetRequiredService<IOidcClientStore>(),
            provider.GetRequiredService<IConfigurationStore>(),
            provider));

        services.AddOidc();

        // LIB-HOST-001, PRIV-RIGHT-005b: what the host declared is read back at
        // startup against the handlers it registered, so the declaration is here as
        // the host wrote it and not only as the model rebuilt it.
        services.AddSingleton(declaration);
        services.AddScoped<IPrivacyAlerts, PrivacyAlerts>();
        services.AddScoped<IDeploymentCookies, DeploymentCookies>();
        services.AddScoped<NoticeCookieCheck>();
        services.AddScoped<ISubjectNotices, SubjectNotices>();

        // AUTHZ-MODEL-001: what may be processed for what is part of the one
        // declaration the host makes, so the privacy side reads it from there rather
        // than asking the host a second time.
        services.AddSingleton(provider =>
            provider.GetRequiredService<AuthorizationModel>().Processing);

        services.AddScoped<IAccessAlerts, AccessAlerts>();

        // LIB-HOST-001, AUTHZ-DERIVE-005: the rows of a relationship are read through the
        // source the host declared for it, from the one instance of the host context
        // the reading scope gives.
        services.AddScoped<IRelationshipSources>(provider => new RelationshipSources(
            provider.GetServices<RelationshipSource>(),
            provider));

        // BFF-ERR-003: what the gate concealed is answered by stage 11 of the same
        // request, so the two share one holder.
        services.AddScoped<ConcealedRefusals>();
        services.AddScoped<IConcealedRefusals>(provider => provider.GetRequiredService<ConcealedRefusals>());

        // IDN-ACCT-007 AC2, AUTHZ-GATE-006: an account's own settings are held under
        // restriction by the gate, which the account's operations ask through a port.
        services.AddScoped<ISettingsRestriction, GatedSettings>();

        // INT-MAIL-001, CONV-DESIGN-007: the shipped adapter is registered as its own
        // type, never as IMailServer; the start chooses it where the host registered no
        // mail server and the endpoint is set.
        services.AddScoped<IAppPasswordLog, AppPasswordLog>();
        services.AddSingleton<JmapMailServer>();
        _ = services.AddHttpClient(JmapMailServer.Channel);

        // INT-MAIL-010: the token the provider issues to the mail server's client, with
        // which the app passwords are reached; without a server there are none.
        services.AddScoped<IMailServerTokens>(provider => new MailServerTokens(
            provider.GetRequiredService<OpenIddict.Server.IOpenIddictServerFactory>(),
            provider.GetRequiredService<OpenIddict.Server.IOpenIddictServerDispatcher>(),
            provider.GetRequiredService<ITokenMinting>(),
            provider.GetRequiredService<IOidcClientStore>(),
            provider.GetService<MailServerClient>(),
            provider.GetRequiredService<AuthenticationAddresses>(),
            provider.GetRequiredService<TimeProvider>()));

        // AUTHZ-MODEL-004 AC2 (D-160): what a hosted service starts before is what was
        // registered after it, and the web server is one, so the checks that read the
        // database go at the head of the collection. OPS-MIG-002 leads them, because
        // every one of the others reads a table, and LIB-HOST-001 follows, because
        // most of them read a key the deployment has to name.
        services.Insert(0, ServiceDescriptor.Singleton<IHostedService, SchemaValidationService>());
        services.Insert(1, ServiceDescriptor.Singleton<IHostedService, SettingsValidationService>());
        services.Insert(2, ServiceDescriptor.Singleton<IHostedService, ModelValidationService>());
        services.Insert(3, ServiceDescriptor.Singleton<IHostedService, SendingValidationService>());

        // CONV-DESIGN-007, D-171, D-176: the key ring's service reads the secrets as the
        // start begins, ahead of every check, and clears the ring once everything has
        // stopped, the worker and the server included. It chooses the mail server in use
        // here, once the stored values have been checked and an endpoint that is not
        // https refused, and before any check that asks which mail server is in use.
        services.Insert(4, KeyRingRegistration.HostedService());
        services.Insert(5, ServiceDescriptor.Singleton<IHostedService, HandlerValidationService>());
        services.Insert(6, ServiceDescriptor.Singleton<IHostedService, ConfigurationValidationService>());
        services.Insert(7, ServiceDescriptor.Singleton<IHostedService, DeclarationValidationService>());
        services.Insert(8, ServiceDescriptor.Singleton<IHostedService, RedirectValidationService>());

        // AUTH-KEY-001, CONV-DESIGN-007, D-181: the provider's start reads the signing
        // keys once the ring is filled, making one current where none is held, and then
        // builds the provider's options.
        services.Insert(9, ServiceDescriptor.Singleton<IHostedService, ProviderStartService>());
        services.Insert(10, ServiceDescriptor.Singleton<IHostedService, RelayValidationService>());

        // PRIV-BASIS-001 (D-183): the declared lawful bases are written into their table
        // once the model and the schema have been checked, and before the server serves.
        services.Insert(11, ServiceDescriptor.Singleton<IHostedService, LawfulBasisStartService>());

        // PRIV-CONS-007 AC5 (D-183): a consent recorded against a document its purpose no
        // longer names is stamped superseded once the declaration has been checked, and
        // before the server serves.
        services.Insert(12, ServiceDescriptor.Singleton<IHostedService, DocumentSupersessionStartService>());

        // PRIV-CONS-006a AC4 (D-203): the cookies the deployment now sets are compared with
        // those the current privacy notice lists once the declaration has been checked.
        services.Insert(13, ServiceDescriptor.Singleton<IHostedService, NoticeCookiesStartService>());

        // AUTHZ-GRANT-004 (D-204): each declared role not yet seeded is defined once the
        // model and the stored roles have been checked, and before the server serves.
        services.Insert(14, ServiceDescriptor.Singleton<IHostedService, DeclaredRoleStartService>());

        // BFF-LOG-002 (D-197): what the host's logging makes of the server's per-request
        // entries is read once the host has written every rule it will, and before the
        // server serves.
        services.Insert(15, ServiceDescriptor.Singleton<IHostedService, RequestLoggingValidationService>());

        // INF-BG-001: the scheduled work starts once the checks above have passed.
        services.AddHostedService(provider => new BackgroundWorker(
            provider.GetRequiredService<IServiceScopeFactory>(),
            BackgroundJobs.All,
            provider.GetRequiredService<TimeProvider>(),
            provider.GetRequiredService<ILogger<BackgroundWorker>>()));

        return services;
    }
}
