using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication;
using Janus.Authentication.Accounts;
using Janus.Authentication.Alerting;
using Janus.Authentication.Configuration;
using Janus.Authentication.Credentials;
using Janus.Authentication.Factors;
using Janus.Authentication.Identifiers;
using Janus.Authentication.Oidc;
using Janus.Authentication.Passwords;
using Janus.Authentication.Policies;
using Janus.Authentication.Recovery;
using Janus.Authentication.Registration;
using Janus.Authentication.Sending;
using Janus.Authentication.Sessions;
using Janus.Authentication.SignIn;
using Janus.Authentication.Tests;
using Janus.Authentication.Tests.Accounts;
using Janus.Authentication.Tests.Alerting;
using Janus.Authentication.Tests.Configuration;
using Janus.Authentication.Tests.Credentials;
using Janus.Authentication.Tests.Factors;
using Janus.Authentication.Tests.Identifiers;
using Janus.Authentication.Tests.Oidc;
using Janus.Authentication.Tests.Passwords;
using Janus.Authentication.Tests.Policies;
using Janus.Authentication.Tests.Recovery;
using Janus.Authentication.Tests.Registration;
using Janus.Authentication.Tests.Sending;
using Janus.Authentication.Tests.Sessions;
using Janus.Authentication.Tests.SignIn;
using Janus.Core;
using Janus.Core.Configuration;
using Janus.Hosting.Accounts;
using Janus.Hosting.Alerting;
using Janus.Hosting.Authentication;
using Janus.Hosting.Bff;
using Janus.Hosting.Configuration;
using Janus.Hosting.Credentials;
using Janus.Hosting.Oidc;
using Janus.Hosting.Privacy;
using Janus.Hosting.Recovery;
using Janus.Hosting.Registration;
using Janus.Hosting.Sending;
using Janus.Hosting.Tests.Bff;
using Janus.Hosting.Tests.Oidc;
using Janus.Privacy;
using Janus.Privacy.Breaches;
using Janus.Privacy.Consents;
using Janus.Privacy.Documents;
using Janus.Privacy.Exports;
using Janus.Privacy.Records;
using Janus.Privacy.Requests;
using Janus.Privacy.Tests.Breaches;
using Janus.Privacy.Tests.Consents;
using Janus.Privacy.Tests.Documents;
using Janus.Privacy.Tests.Exports;
using Janus.Privacy.Tests.Outbox;
using Janus.Privacy.Tests.Requests;
using Janus.Storage.Authentication.Oidc;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenIddict.Abstractions;

namespace Janus.Hosting.Tests;

/// <summary>
/// The library mounted the way a host mounts it, over fakes instead of a database,
/// with the pipeline of BFF-ORDER-001 in front of it. What a test sends goes through
/// routing, the middleware and the endpoint exactly as a browser's request does.
/// </summary>
internal sealed class Deployment : IAsyncDisposable
{
    private static readonly DateTimeOffset Noon = new(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);

    // AUTH-KEY-002, OPS-SEC-001: what the codes and the refresh tokens the provider
    // writes are encrypted under, which a deployment is handed and never generates.
    private static readonly KeyEncryptionKeys Wrapping = new(
        1,
        new Dictionary<int, ReadOnlyMemory<byte>> { [1] = new byte[32] });

    private readonly RandomNumberGenerator _randomness = RandomNumberGenerator.Create();
    private readonly WebApplication _application;
    private readonly RequestDelegate _pipeline;

    // LIB-HOST-001: where a browser holding no session is sent is a declaration no
    // deployment starts without, so every deployment here carries one (AUTH-SESS-012).
    private static readonly AuthenticationAddresses Screen = new(
        "https://identity.example.test/signin",
        "https://identity.example.test");

    // LIB-HOST-001, BFF-SESS-006: which client of the provider this application is
    // is a declaration no deployment starts without either.
    private static readonly SignOnClient Registered = new("this-application");

    // LIB-HOST-001, INT-MAIL-010: a deployment that registers a mail server declares
    // which client of the provider the server is, and every deployment here registers
    // one.
    private static readonly MailServerClient MailClient = new("mail-server");

    // LIB-HOST-001: the frontend's pages are a declaration no deployment starts
    // without, so every deployment here carries one (REG-PM-001).
    private static readonly PasskeyAddresses Pages = new(
        "https://accounts.example.test/password",
        "https://accounts.example.test/passkeys/new",
        "https://accounts.example.test/passkeys");

    /// <summary>
    /// Mounts the library over fakes.
    /// </summary>
    /// <param name="application">Which application this process serves.</param>
    /// <param name="addresses">The frontend addresses the host declared.</param>
    /// <param name="prefix">The path the host mounts the library under.</param>
    /// <param name="preferences">The preference keys the host declared.</param>
    /// <param name="signIn">Where the host's own sign-in screen is.</param>
    /// <param name="client">Which client of the provider this application is.</param>
    public Deployment(
        ApplicationKind application = ApplicationKind.Public,
        PasskeyAddresses? addresses = null,
        string prefix = "",
        PreferenceDeclarations? preferences = null,
        AuthenticationAddresses? signIn = null,
        SignOnClient? client = null)
    {
        WebApplicationBuilder builder = WebApplication.CreateSlimBuilder();

        builder.Logging.ClearProviders();

        Signals = new RegistrationSignalsInMemory(Clock);
        Grants = new OidcAuthorizationStoreInMemory(Tokens);
        Provider = new ProviderInMemory(this);
        Organizations = new Janus.Authentication.Tests.Organizations.OrganizationsInMemory(Memberships);
        Attachments = new Janus.Authentication.Tests.Invitations.MembershipAttachmentInMemory(Memberships);
        Endings = new Janus.Authentication.Tests.Invitations.MembershipEndingInMemory(Memberships);

        Declared = preferences ?? PreferenceDeclarations.None;
        Accounts = new AccountDirectoryInMemory(Declared);

        // Two of the keys a deployment names or does not start, which a ceremony and
        // the challenge every sign-in carries are read from (OPS-CFG-001).
        Configuration.Set(Settings.WebAuthnRelyingPartyId, "identity.example.test");
        Configuration.Set(Settings.WebAuthnOrigins, ["https://identity.example.test"]);

        Register(
            builder.Services,
            application,
            addresses ?? Pages,
            signIn ?? Screen,
            client ?? Registered);

        _application = builder.Build();

        // The web server composes these two around the middleware when it starts;
        // here the pipeline is built by hand, so they are named by hand.
        if (prefix.Length > 0)
        {
            // A host that mounts the library under a prefix mounts all of it there,
            // the provider's endpoints with the rest, so every path the library
            // answers and every address its document publishes carries the prefix
            // (API-CONV-001, LIB-HOST-003). The two documents of REG-PM-001 are the
            // exception: they belong to the site and stay at its root.
            _ = ((IApplicationBuilder)_application).Map(prefix, Mounted);
        }
        else
        {
            Mounted(_application);
        }

        _ = ((IApplicationBuilder)_application).UseRouting();
        _ = _application.MapIdentityWellKnown();
        _ = ((IApplicationBuilder)_application).UseEndpoints(_ => { });

        _pipeline = ((IApplicationBuilder)_application).Build();

        // AUTH-KEY-001: the server is put together with the key the store holds at
        // startup, which is what the hosted service of the same name does in a
        // deployment that a web server starts.
        using (IServiceScope scope = _application.Services.CreateScope())
        {
            _ = _application.Services
                .GetRequiredService<SigningCredentialSource>()
                .CurrentAsync(
                    scope.ServiceProvider.GetRequiredService<SigningKeys>(),
                    CancellationToken.None)
                .AsTask()
                .GetAwaiter()
                .GetResult();
        }
    }

    /// <summary>
    /// The provider this application's back channel reaches, which is this same
    /// deployment (BFF-SESS-006 AC2).
    /// </summary>
    public ProviderInMemory Provider { get; }

    /// <summary>
    /// The clock the whole deployment reads.
    /// </summary>
    public FixedClock Clock { get; } = new(Noon);

    /// <summary>
    /// The settings table, for the keys a test switches.
    /// </summary>
    public ConfigurationInMemory Configuration { get; } = new();

    /// <summary>
    /// Which organizations a principal belongs to.
    /// </summary>
    public MembershipLookupInMemory Memberships { get; } = new();

    /// <summary>
    /// What each policy has raised, by scope.
    /// </summary>
    public PolicyRaiseStoreInMemory Raises { get; } = new();

    /// <summary>
    /// What an uploaded image is read and re-encoded by.
    /// </summary>
    public ImageCodecInMemory Codec { get; } = new();

    /// <summary>
    /// What went out by mail.
    /// </summary>
    public MailTransportInMemory Mail { get; } = new();

    /// <summary>
    /// What went out by SMS.
    /// </summary>
    public SmsTransportInMemory Sms { get; } = new();

    /// <summary>
    /// The registration sessions as they stand.
    /// </summary>
    public RegistrationSessionStoreInMemory Registrations { get; } = new();

    /// <summary>
    /// The channel the waiting screen's stream waits on.
    /// </summary>
    public RegistrationSignalsInMemory Signals { get; }

    /// <summary>
    /// The accounts registration created.
    /// </summary>
    public RegistrationDirectoryInMemory Directory { get; } = new();

    /// <summary>
    /// The pre-authentication sessions as they stand.
    /// </summary>
    public PreAuthenticationStoreInMemory Contacts { get; } = new();

    /// <summary>
    /// The authenticated sessions as they stand.
    /// </summary>
    public SessionStoreInMemory Sessions { get; } = new();

    /// <summary>
    /// The identifiers the accounts hold.
    /// </summary>
    public IdentifierDirectoryInMemory Identifiers { get; } = new();

    /// <summary>
    /// The verifications outstanding on an account's identifiers.
    /// </summary>
    public PendingVerificationStoreInMemory Pending { get; } = new();

    /// <summary>
    /// The standing, profile and preferences of the accounts.
    /// </summary>
    public AccountDirectoryInMemory Accounts { get; }

    /// <summary>
    /// The preference keys the host declared.
    /// </summary>
    public PreferenceDeclarations Declared { get; }

    /// <summary>
    /// The templates every message is written from.
    /// </summary>
    public MessageTemplatesInMemory Templates { get; } = new();

    /// <summary>
    /// The credentials the accounts hold.
    /// </summary>
    public AuthenticatorStoreInMemory Authenticators { get; } = new();

    /// <summary>
    /// The passwords the accounts hold.
    /// </summary>
    public PasswordStoreInMemory Passwords { get; } = new();

    /// <summary>
    /// The recovery links that have gone out.
    /// </summary>
    public RecoveryLinkStoreInMemory Links { get; } = new();

    /// <summary>
    /// The clients the deployment registered.
    /// </summary>
    public OidcClientStoreInMemory Clients { get; } = new();

    /// <summary>
    /// The legal documents the deployment published.
    /// </summary>
    public LegalDocumentStoreInMemory Documents { get; } = new();

    /// <summary>
    /// The consent and objection records the deployment holds.
    /// </summary>
    public ConsentStoreInMemory Consents { get; } = new();

    /// <summary>
    /// The gate, so a test can grant what an administrative path asks for.
    /// </summary>
    public AccessGateInMemory Gate { get; } = new();

    /// <summary>
    /// The administrative organization as the authentication area reads it.
    /// </summary>
    private AdministrativeOrganizationInMemory Administrative { get; } = new();

    /// <summary>
    /// The administrative organization as the privacy area reads it.
    /// </summary>
    private Janus.Privacy.Tests.AdministrativeOrganizationInMemory PrivacyAdministrative { get; } = new();

    /// <summary>
    /// The data subject request queue, so a test can read what was put on it.
    /// </summary>
    public PrivacyRequestStoreInMemory Requests { get; } = new();

    /// <summary>
    /// The account states the privacy area moves.
    /// </summary>
    public AccountStatesInMemory AccountStates { get; } = new();

    /// <summary>
    /// The outbox, so a test can read what was announced.
    /// </summary>
    public OutboxStoreInMemory Outbox { get; } = new();

    /// <summary>
    /// What the other areas hold of an export, so a test can arrange it.
    /// </summary>
    public ExportSourceInMemory ExportSource { get; } = new();

    /// <summary>
    /// The exports the accounts have taken, so a test can read what was counted.
    /// </summary>
    public ExportLedgerInMemory ExportLedger { get; } = new();

    /// <summary>
    /// The three fields the deployment supplies to the records of processing.
    /// </summary>
    public Janus.Privacy.Tests.Records.ComplianceStoreInMemory Compliance { get; } = new();

    /// <summary>
    /// The roles of the deployment, so a test can say what each allows.
    /// </summary>
    public Janus.Privacy.Tests.Records.RegisterRolesInMemory RegisterRoles { get; } = new();

    /// <summary>
    /// What the subject was told.
    /// </summary>
    public SubjectNoticesInMemory Notices { get; } = new();

    /// <summary>
    /// The codes and tokens the provider issued.
    /// </summary>
    public OidcTokenStoreInMemory Tokens { get; } = new();

    /// <summary>
    /// The grants the codes and tokens hang from.
    /// </summary>
    public OidcAuthorizationStoreInMemory Grants { get; }

    /// <summary>
    /// The scopes the deployment registered beyond the ones the provider is built with.
    /// </summary>
    public OidcScopeStoreInMemory Scopes { get; } = new();

    /// <summary>
    /// The signing keys the deployment holds.
    /// </summary>
    public SigningKeyStoreInMemory Keys { get; } = new();

    /// <summary>
    /// What the provider recorded.
    /// </summary>
    public OidcAuditInMemory OidcAudit { get; } = new();

    /// <summary>
    /// What the provider logged about a request it corrected.
    /// </summary>
    public LogInMemory<RegisteredDestination> OidcLog { get; } = new();

    /// <summary>
    /// What the sign-on recorded when it would not carry a return (BFF-SESS-006 AC3).
    /// </summary>
    public LogInMemory<SignOn> SignOnLog { get; } = new();

    /// <summary>
    /// Every endpoint the library mounted.
    /// </summary>
    public IReadOnlyList<Endpoint> Endpoints =>
        _application.Services.GetRequiredService<EndpointDataSource>().Endpoints;

    /// <summary>
    /// The transaction every operation runs in.
    /// </summary>
    public UnitOfWorkInMemory Work { get; } = new();

    /// <summary>
    /// The events the operations published.
    /// </summary>
    public EventsInMemory Events { get; } = new();

    /// <summary>
    /// The audit trail as the privacy area reads it by subject.
    /// </summary>
    public AuditTrailStoreInMemory Trail { get; } = new();

    /// <summary>
    /// The record of every runtime configuration change.
    /// </summary>
    public ConfigurationAuditInMemory Changes { get; } = new();

    /// <summary>
    /// The alerts the deployment raised.
    /// </summary>
    public AlertLedgerInMemory Alerts { get; } = new();

    /// <summary>
    /// The stored grants the deployment holds.
    /// </summary>
    public Janus.Authorization.Tests.Gate.GrantsInMemory AccessGrants { get; } = new();

    /// <summary>
    /// The roles the deployment holds.
    /// </summary>
    public Janus.Authorization.Tests.Roles.RolesInMemory Roles { get; } = new();

    /// <summary>
    /// The groups the deployment holds.
    /// </summary>
    public Janus.Authorization.Tests.Gate.GroupsInMemory Groups { get; } = new();

    /// <summary>
    /// The changes to roles the deployment wrote down.
    /// </summary>
    public Janus.Authorization.Tests.Roles.RoleAuditInMemory RoleChanges { get; } = new();

    /// <summary>
    /// The changes to groups the deployment wrote down.
    /// </summary>
    public Janus.Authorization.Tests.Groups.GroupAuditInMemory GroupChanges { get; } = new();

    /// <summary>
    /// The organizations the deployment holds, their members those placed in
    /// <see cref="Memberships"/>.
    /// </summary>
    public Janus.Authentication.Tests.Organizations.OrganizationsInMemory Organizations { get; }

    /// <summary>
    /// The changes to organizations the deployment wrote down.
    /// </summary>
    public Janus.Authentication.Tests.Organizations.OrganizationAuditInMemory OrganizationChanges { get; } = new();

    /// <summary>
    /// The invitations the organizations issued.
    /// </summary>
    public Janus.Authentication.Tests.Invitations.InvitationStoreInMemory Invitations { get; } = new();

    /// <summary>
    /// The memberships the acknowledged invitations attached.
    /// </summary>
    public Janus.Authentication.Tests.Invitations.MembershipAttachmentInMemory Attachments { get; }

    /// <summary>
    /// The memberships administrators ended.
    /// </summary>
    public Janus.Authentication.Tests.Invitations.MembershipEndingInMemory Endings { get; }

    /// <summary>
    /// The roles an invitation may name.
    /// </summary>
    public Janus.Authentication.Tests.Invitations.RoleCatalogueInMemory RoleCatalogue { get; } = new();

    /// <summary>
    /// The mailboxes the invitations reserved.
    /// </summary>
    public Janus.Authentication.Tests.Mailboxes.MailboxStoreInMemory Mailboxes { get; } = new();

    /// <summary>
    /// The mail server the administrative organization's mail is integrated with.
    /// </summary>
    public Janus.Authentication.Tests.Mailboxes.MailServerInMemory MailServer { get; } = new();

    /// <summary>
    /// The domains organizations lock their members to.
    /// </summary>
    public Janus.Authentication.Tests.Organizations.DomainStoreInMemory Domains { get; } = new();

    /// <summary>
    /// The TXT records the deployment's resolver answers.
    /// </summary>
    public Janus.Authentication.Tests.Organizations.DnsResolverInMemory Dns { get; } = new();

    /// <summary>
    /// The records the host registered.
    /// </summary>
    public Janus.Authorization.Tests.Resources.ResourcesInMemory Resources { get; } = new();

    /// <summary>
    /// The administrative organization as the authorization area reads it.
    /// </summary>
    private Janus.Authorization.Tests.Gate.AdministrativeOrganizationInMemory GateAdministrative { get; } = new();

    /// <summary>
    /// Names the organization that administers the deployment, as bootstrap does, so a
    /// permission granted there is one an administrative operation honours.
    /// </summary>
    /// <param name="organization">The organization.</param>
    public void Administers(OrganizationId organization)
    {
        Administrative.Organization = organization;
        PrivacyAdministrative.Organization = organization;
        GateAdministrative.Organization = organization;
        Gate.Administrative = organization;
        Organizations.Seed(organization, administrative: true);
    }

    /// <summary>
    /// Runs one request through routing, the pipeline and the endpoint, in a scope
    /// of its own as the web server would.
    /// </summary>
    /// <param name="context">The request as the browser wrote it.</param>
    /// <returns>The work of answering it.</returns>
    /// <exception cref="ArgumentNullException">The request is absent.</exception>
    public async Task SendAsync(DefaultHttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        await using AsyncServiceScope scope = _application.Services.CreateAsyncScope();

        context.RequestServices = scope.ServiceProvider;

        await _pipeline(context);
    }

    // What a host mounts: the two profiles around the library's endpoints, with
    // routing named by hand because no web server composes it here.
    private static void Mounted(IApplicationBuilder mount)
    {
        _ = mount.UseRouting();
        _ = mount.UseMachineProfile();
        _ = mount.UseBrowserProfile();
        _ = mount.UseEndpoints(endpoints => endpoints.MapIdentityEndpoints());
    }

    // Everything AddJanus registers, over the area's own fakes instead of the
    // database: the ports, the services built on them and the browser boundary.
    private void Register(
        IServiceCollection services,
        ApplicationKind application,
        PasskeyAddresses addresses,
        AuthenticationAddresses signIn,
        SignOnClient client)
    {
        _ = services.AddSingleton<TimeProvider>(Clock);
        _ = services.AddSingleton(_randomness);
        _ = services.AddSingleton<IConfigurationStore>(Configuration);
        _ = services.AddSingleton<IUnitOfWork>(Work);
        _ = services.AddSingleton<IEvents>(Events);

        _ = services.AddSingleton<IRegistrationSessionStore>(Registrations);
        _ = services.AddSingleton<IRegistrationSignals>(Signals);
        _ = services.AddSingleton<IRegistrationDirectory>(Directory);
        _ = services.AddSingleton<IPreAuthenticationStore>(Contacts);
        _ = services.AddSingleton<ISessionStore>(Sessions);
        _ = services.AddSingleton<IIdentifierDirectory>(Identifiers);
        _ = services.AddSingleton<IPendingVerificationStore>(Pending);
        _ = services.AddSingleton<IAccountDirectory>(Accounts);
        _ = services.AddSingleton<IMessageTemplates>(Templates);
        _ = services.AddSingleton<IMailTransport>(Mail);
        _ = services.AddSingleton<ISmsTransport>(Sms);
        _ = services.AddSingleton<IAuthenticatorStore>(Authenticators);
        _ = services.AddSingleton<IPasswordStore>(Passwords);

        _ = services.AddSingleton<ISendLedger, SendLedgerInMemory>();
        _ = services.AddSingleton<ISendOutbox, SendOutboxInMemory>();
        _ = services.AddSingleton<INoticeLedger, NoticeLedgerInMemory>();
        _ = services.AddSingleton<ISmsBalanceLedger, SmsBalanceLedgerInMemory>();
        _ = services.AddSingleton<ILeakedPasswordCorpus, LeakedPasswordCorpusInMemory>();
        _ = services.AddSingleton<IWordList, WordListInMemory>();
        _ = services.AddSingleton<IScreeningLog, ScreeningLogInMemory>();
        _ = services.AddSingleton<IRecoveryCodeStore, RecoveryCodeStoreInMemory>();
        _ = services.AddSingleton<IDeviceStore, DeviceStoreInMemory>();
        _ = services.AddSingleton<ISessionAudit, SessionAuditInMemory>();
        _ = services.AddSingleton<IMembershipLookup>(Memberships);
        _ = services.AddSingleton(Codec.Declared);
        _ = services.AddSingleton<IPolicyRaiseStore>(Raises);
        _ = services.AddSingleton<IChallengeStore, ChallengeStoreInMemory>();
        _ = services.AddSingleton<IVerificationCodeStore, VerificationCodeStoreInMemory>();
        _ = services.AddSingleton<IPendingSignInStore, PendingSignInStoreInMemory>();
        _ = services.AddSingleton<IAccessGate>(Gate);
        _ = services.AddSingleton<IAccountAudit, AccountAuditInMemory>();
        _ = services.AddSingleton<ILifecycleLinkStore, LifecycleLinkStoreInMemory>();
        _ = services.AddSingleton<IRecoveryLinkStore>(Links);
        _ = services.AddSingleton<IRecoveryApprovalStore, RecoveryApprovalStoreInMemory>();
        _ = services.AddSingleton<ILossReportStore, LossReportStoreInMemory>();
        _ = services.AddSingleton<IRecoveryAudit, RecoveryAuditInMemory>();
        _ = services.AddSingleton<IKeyCeremonyStore, KeyCeremonyStoreInMemory>();
        _ = services.AddSingleton<IOidcClientStore>(Clients);
        _ = services.AddSingleton<ISigningKeyStore>(Keys);
        _ = services.AddSingleton<IOidcAudit>(OidcAudit);
        _ = services.AddSingleton<ILogger<RegisteredDestination>>(OidcLog);
        _ = services.AddSingleton<ILogger<SignOn>>(SignOnLog);

        // The records the protocol server keeps are the library's rows, so a
        // deployment that runs over fakes holds them the way it holds every other
        // table (D-162, CONV-TEST-002).
        _ = services.AddSingleton<IOpenIddictApplicationStore<OidcClientRecord>>(
            new OidcApplicationStoreInMemory(Clients));
        _ = services.AddSingleton<IOpenIddictAuthorizationStore<OidcAuthorizationRecord>>(Grants);
        _ = services.AddSingleton<IOpenIddictScopeStore<OidcScopeRecord>>(Scopes);
        _ = services.AddSingleton<IOpenIddictTokenStore<OidcTokenRecord>>(Tokens);

        _ = services.AddSingleton(RestrictionKeySuppliers.None);
        _ = services.AddSingleton(Declared);
        _ = services.AddSingleton(ReservedUsernames.Default);
        _ = services.AddSingleton(addresses);
        _ = services.AddSingleton(signIn);
        _ = services.AddSingleton(client);
        _ = services.AddSingleton(MailClient);

        // BFF-SESS-006: the client half of the sign-on is the library's, and the
        // connection it trades a code on reaches this same deployment's machine
        // profile, which is what a second application's back channel reaches.
        _ = services.AddSingleton(new SignOnSecret(
            Encoding.UTF8.GetBytes("a-secret-the-deployment-set")));
        _ = services.AddScoped<SignOn>();
        _ = services.AddHttpClient(SignOn.Channel)
            .ConfigurePrimaryHttpMessageHandler(() => Provider);

        _ = services.AddScoped<SmsBalance>();
        _ = services.AddScoped<SendingService>();
        _ = services.AddScoped<INotificationHandler>(
            provider => provider.GetRequiredService<SendingService>());
        _ = services.AddSingleton<IPhoneSignalAudit, PhoneSignalAuditInMemory>();
        _ = services.AddScoped(provider => new PhoneSignals(
            provider.GetService<PhoneSignalProvider>(),
            provider.GetRequiredService<IPhoneSignalAudit>(),
            provider.GetRequiredService<IUnitOfWork>(),
            provider.GetRequiredService<TimeProvider>()));
        _ = services.AddScoped<NonExistenceNotice>();
        _ = services.AddScoped<ThrottleService>();
        _ = services.AddSingleton<IThrottleLedger, ThrottleLedgerInMemory>();
        _ = services.AddSingleton<ICredentialAudit, CredentialAuditInMemory>();
        _ = services.AddSingleton<Argon2idHasher>();
        _ = services.AddScoped<PasswordScreening>();
        _ = services.AddScoped<PasswordService>();
        _ = services.AddScoped<RecoveryCodeService>();
        _ = services.AddScoped<DeviceService>();
        _ = services.AddScoped<PolicyResolution>();
        _ = services.AddSingleton<IAdministrativeOrganization>(Administrative);
        _ = services.AddScoped<AdministrativeScope>();
        _ = services.AddScoped<StepUpGuard>();
        _ = services.AddScoped<IStepUpGate, StepUpGate>();
        _ = services.AddScoped<ILocationResolver, LocationResolverInMemory>();
        _ = services.AddScoped<SessionService>();
        _ = services.AddScoped<ISessions>(provider => provider.GetRequiredService<SessionService>());
        _ = services.AddScoped<PreAuthenticationService>();
        _ = services.AddScoped<RegistrationService>();
        _ = services.AddScoped<IRegistration>(provider =>
            provider.GetRequiredService<RegistrationService>());
        _ = services.AddScoped<IdentifierService>();
        _ = services.AddScoped<IIdentifiers>(provider =>
            provider.GetRequiredService<IdentifierService>());
        _ = services.AddScoped<AccountLifecycle>();
        _ = services.AddScoped(provider => new ProfilePhotos(
            provider.GetRequiredService<IAccountDirectory>(),
            provider.GetRequiredService<IMembershipLookup>(),
            provider.GetRequiredService<IConfigurationStore>(),
            provider.GetRequiredService<IAccountAudit>(),
            provider.GetRequiredService<IUnitOfWork>(),
            provider.GetRequiredService<ImageCodec>(),
            provider.GetRequiredService<TimeProvider>()));
        _ = services.AddScoped<AccountService>();
        _ = services.AddScoped<IAccount>(provider => provider.GetRequiredService<AccountService>());
        _ = services.AddScoped<IAccounts, AccountAdministration>();
        _ = services.AddScoped<TotpService>();
        _ = services.AddScoped<WebAuthnService>();
        _ = services.AddScoped<SignInLinks>();
        _ = services.AddScoped<VerificationCodes>();
        _ = services.AddScoped<AuthenticationService>();
        _ = services.AddScoped<IAuthentication>(provider =>
            provider.GetRequiredService<AuthenticationService>());
        _ = services.AddScoped<LossReports>();
        _ = services.AddScoped<EnrolmentSessions>();
        _ = services.AddScoped<RecoveryService>();
        _ = services.AddScoped<IRecovery>(provider => provider.GetRequiredService<RecoveryService>());
        _ = services.AddScoped<ICredentials, CredentialService>();
        _ = services.AddSingleton<ILegalDocumentStore>(Documents);
        _ = services.AddSingleton<IPrivacyAudit, Janus.Privacy.Tests.PrivacyAuditInMemory>();
        _ = services.AddSingleton<Janus.Privacy.Policies.IAdministrativeOrganization>(PrivacyAdministrative);
        _ = services.AddScoped<IPrivacyAlerts, PrivacyAlerts>();
        _ = services.AddScoped<Janus.Privacy.Policies.AdministrativeScope>();
        _ = services.AddScoped<ILegalDocuments, LegalDocumentService>();
        _ = services.AddSingleton<IConsentStore>(Consents);
        _ = services.AddSingleton(Janus.Privacy.Tests.Declaration.Processing);
        _ = services.AddScoped<Supersession>();
        _ = services.AddScoped<IConsents, ConsentService>();
        _ = services.AddSingleton<IPrivacyRequestStore>(Requests);
        _ = services.AddSingleton<IAccountStates>(AccountStates);
        _ = services.AddSingleton<Janus.Privacy.Outbox.IOutboxStore>(Outbox);
        _ = services.AddSingleton<ISubjectNotices>(Notices);
        _ = services.AddScoped<WorkingCalendar>();
        _ = services.AddScoped<RestrictionGrant>();
        _ = services.AddScoped<DeadlineSweep>();
        _ = services.AddScoped<IPrivacyRequests, PrivacyRequestService>();
        _ = services.AddSingleton<IExportSource>(ExportSource);
        _ = services.AddSingleton<IExportLedger>(ExportLedger);
        _ = services.AddScoped<IExports, ExportService>();
        _ = services.AddScoped<ITakedowns, Janus.Privacy.Takedowns.TakedownService>();
        _ = services.AddSingleton(Janus.Privacy.Tests.Declaration.Reaching);
        _ = services.AddSingleton<Janus.Privacy.Records.IComplianceStore>(Compliance);
        _ = services.AddSingleton<Janus.Privacy.Records.IRegisterRoles>(RegisterRoles);
        _ = services.AddScoped<IProcessingRecords, ProcessingRecordsService>();
        _ = services.AddSingleton<IAuditTrailStore>(Trail);
        _ = services.AddScoped<IAuditTrail, AuditTrailService>();
        _ = services.AddSingleton<IConfigurationAudit>(Changes);
        _ = services.AddScoped<ConfigurationAdministration>();
        _ = services.AddSingleton<IAlertLedger>(Alerts);
        _ = services.AddSingleton<IAlertLog, AlertLogInMemory>();
        _ = services.AddScoped<AlertRouter>();
        _ = services.AddScoped<AlertDestinationChange>();
        _ = services.AddScoped<IConfigurationAdministration, ConfigurationService>();
        _ = services.AddSingleton<ISendAudit, SendAuditInMemory>();
        _ = services.AddScoped<RestrictionAdministration>();
        _ = services.AddScoped<IRestrictionSet, RestrictionSetService>();
        _ = services.AddSingleton<Janus.Authorization.Gate.IAdministrativeOrganization>(GateAdministrative);
        _ = services.AddSingleton<Janus.Authorization.Grants.IGrantStore>(AccessGrants);
        _ = services.AddSingleton<Janus.Authorization.Roles.IRoleStore>(Roles);
        _ = services.AddSingleton<Janus.Authorization.Groups.IGroupStore>(Groups);
        _ = services.AddSingleton<Janus.Authorization.Resources.IResourceStore>(Resources);
        _ = services.AddSingleton(Janus.Authorization.Model.AuthorizationModel.Of(
            Janus.Authorization.Tests.HostDomain.Declared().Build()));
        _ = services.AddSingleton<Janus.Authorization.Roles.IRoleAudit>(RoleChanges);
        _ = services.AddSingleton<Janus.Authorization.Groups.IGroupAudit>(GroupChanges);
        _ = services.AddScoped<Janus.Authorization.Gate.AdministrativeScope>();
        _ = services.AddScoped<IGrants, Janus.Authorization.Grants.GrantService>();
        _ = services.AddScoped<IRoles, Janus.Authorization.Roles.RoleService>();
        _ = services.AddScoped<IGroups, Janus.Authorization.Groups.GroupService>();
        _ = services.AddSingleton<Janus.Authentication.Organizations.IOrganizationDirectory>(Organizations);
        _ = services.AddSingleton<Janus.Authentication.Organizations.IOrganizationAudit>(OrganizationChanges);
        _ = services.AddScoped<IOrganizations, Janus.Authentication.Organizations.OrganizationService>();
        _ = services.AddSingleton<Janus.Authentication.Organizations.IDomainStore>(Domains);
        _ = services.AddSingleton<IDnsResolver>(Dns);
        _ = services.AddScoped<Janus.Authentication.Organizations.DomainLock>();
        _ = services.AddScoped<Janus.Authentication.Organizations.DomainReverification>();
        _ = services.AddScoped<IOrganizationDomains, Janus.Authentication.Organizations.OrganizationDomainService>();
        _ = services.AddSingleton<Janus.Authentication.Invitations.IInvitationStore>(Invitations);
        _ = services.AddSingleton<Janus.Authentication.Invitations.IRoleCatalogue>(RoleCatalogue);
        _ = services.AddSingleton<Janus.Authentication.Mailboxes.IMailboxStore>(Mailboxes);
        _ = services.AddSingleton<IMailServer>(MailServer);
        _ = services.AddSingleton<Janus.Authentication.Invitations.IMembershipAttachment>(Attachments);
        _ = services.AddScoped<Janus.Authentication.Invitations.InvitationAcknowledgement>();
        _ = services.AddSingleton<Janus.Authentication.Invitations.IMembershipEnding>(Endings);
        _ = services.AddScoped<Janus.Authentication.Invitations.MembershipEnd>();
        _ = services.AddScoped<IInvitations, Janus.Authentication.Invitations.InvitationService>();
        _ = services.AddScoped<SigningKeys>();
        _ = services.AddScoped<OidcService>();
        _ = services.AddScoped<IOidc>(provider => provider.GetRequiredService<OidcService>());
        _ = services.AddOidc(Wrapping);
        _ = services.AddScoped<Janus.Authentication.Mailboxes.IMailServerTokens, Janus.Hosting.Oidc.MailServerTokens>();
        _ = services.AddScoped<IAppPasswords, Janus.Authentication.Mailboxes.AppPasswords>();

        _ = services.AddSingleton(new BrowserSessionCookies(application));
        _ = services.AddScoped<SynchronizerTokens>();
        _ = services.AddScoped<MalformedRequest>();
        _ = services.AddScoped<ResourceIsolation>();
        _ = services.AddScoped<CustomRequestHeader>();
        _ = services.AddScoped<OriginValidation>();
        _ = services.AddScoped<RequestSession>();
        _ = services.AddScoped<SessionResolution>();
        _ = services.AddScoped<FirstContact>();
        _ = services.AddScoped<SynchronizerToken>();
        _ = services.AddScoped<SessionRequirement>();
        _ = services.AddScoped<MachineProfile>();

        _ = services.Configure<RouteHandlerOptions>(options => options.ThrowOnBadRequest = true);

        _ = services.ConfigureHttpJsonOptions(HostingRegistration.ReadThroughContexts);
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await _application.DisposeAsync();
        await Work.DisposeAsync();

        _randomness.Dispose();
    }
}
