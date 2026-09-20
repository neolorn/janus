using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Janus.Authentication;
using Janus.Authentication.Accounts;
using Janus.Authentication.Factors;
using Janus.Authentication.Identifiers;
using Janus.Authentication.Passwords;
using Janus.Authentication.Policies;
using Janus.Authentication.Registration;
using Janus.Authentication.Sending;
using Janus.Authentication.Sessions;
using Janus.Authentication.SignIn;
using Janus.Authentication.Tests;
using Janus.Authentication.Tests.Accounts;
using Janus.Authentication.Tests.Factors;
using Janus.Authentication.Tests.Identifiers;
using Janus.Authentication.Tests.Passwords;
using Janus.Authentication.Tests.Policies;
using Janus.Authentication.Tests.Registration;
using Janus.Authentication.Tests.Sending;
using Janus.Authentication.Tests.Sessions;
using Janus.Authentication.Tests.SignIn;
using Janus.Core;
using Janus.Core.Configuration;
using Janus.Hosting.Accounts;
using Janus.Hosting.Authentication;
using Janus.Hosting.Bff;
using Janus.Hosting.Registration;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Janus.Hosting.Tests;

/// <summary>
/// The library mounted the way a host mounts it, over fakes instead of a database,
/// with the pipeline of BFF-ORDER-001 in front of it. What a test sends goes through
/// routing, the middleware and the endpoint exactly as a browser's request does.
/// </summary>
internal sealed class Deployment : IAsyncDisposable
{
    private static readonly DateTimeOffset Noon = new(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly RandomNumberGenerator _randomness = RandomNumberGenerator.Create();
    private readonly WebApplication _application;
    private readonly RequestDelegate _pipeline;

    /// <summary>
    /// Mounts the library over fakes.
    /// </summary>
    /// <param name="application">Which application this process serves.</param>
    /// <param name="addresses">The frontend addresses the host declared.</param>
    /// <param name="prefix">The path the host mounts the library under.</param>
    /// <param name="preferences">The preference keys the host declared.</param>
    public Deployment(
        JanusApplication application = JanusApplication.Public,
        PasskeyAddresses? addresses = null,
        string prefix = "",
        PreferenceDeclarations? preferences = null)
    {
        WebApplicationBuilder builder = WebApplication.CreateSlimBuilder();

        builder.Logging.ClearProviders();

        Declared = preferences ?? PreferenceDeclarations.None;
        Accounts = new AccountDirectoryInMemory(Declared);

        // Two of the keys a deployment names or does not start, which a ceremony and
        // the challenge every sign-in carries are read from (OPS-CFG-001).
        Configuration.Set(Settings.WebAuthnRelyingPartyId, "janus.example.test");
        Configuration.Set(Settings.WebAuthnOrigins, ["https://janus.example.test"]);

        Register(builder.Services, application, addresses ?? PasskeyAddresses.None);

        _application = builder.Build();

        // The web server composes these two around the middleware when it starts;
        // here the pipeline is built by hand, so they are named by hand.
        _ = ((IApplicationBuilder)_application).UseRouting();
        _ = _application.UseJanusBrowserProfile();
        _ = _application.MapGroup(prefix).MapJanus();
        _ = _application.MapJanusWellKnown();
        _ = ((IApplicationBuilder)_application).UseEndpoints(_ => { });

        _pipeline = ((IApplicationBuilder)_application).Build();
    }

    /// <summary>
    /// The clock the whole deployment reads.
    /// </summary>
    public FixedClock Clock { get; } = new(Noon);

    /// <summary>
    /// The settings table, for the keys a test switches.
    /// </summary>
    public ConfigurationInMemory Configuration { get; } = new();

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

    // Everything AddJanus registers, over the area's own fakes instead of the
    // database: the ports, the services built on them and the browser boundary.
    private void Register(
        IServiceCollection services,
        JanusApplication application,
        PasskeyAddresses addresses)
    {
        _ = services.AddSingleton<TimeProvider>(Clock);
        _ = services.AddSingleton(_randomness);
        _ = services.AddSingleton<IConfigurationStore>(Configuration);
        _ = services.AddSingleton<IUnitOfWork>(Work);
        _ = services.AddSingleton<IEvents>(Events);

        _ = services.AddSingleton<IRegistrationSessionStore>(Registrations);
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
        _ = services.AddSingleton<INoticeLedger, NoticeLedgerInMemory>();
        _ = services.AddSingleton<ISmsBalanceLedger, SmsBalanceLedgerInMemory>();
        _ = services.AddSingleton<ILeakedPasswordCorpus, LeakedPasswordCorpusInMemory>();
        _ = services.AddSingleton<IWordList, WordListInMemory>();
        _ = services.AddSingleton<IScreeningLog, ScreeningLogInMemory>();
        _ = services.AddSingleton<IRecoveryCodeStore, RecoveryCodeStoreInMemory>();
        _ = services.AddSingleton<IDeviceStore, DeviceStoreInMemory>();
        _ = services.AddSingleton<ISessionAudit, SessionAuditInMemory>();
        _ = services.AddSingleton<IMembershipLookup, MembershipLookupInMemory>();
        _ = services.AddSingleton<IPolicyRaiseStore, PolicyRaiseStoreInMemory>();
        _ = services.AddSingleton<IChallengeStore, ChallengeStoreInMemory>();
        _ = services.AddSingleton<IPendingSignInStore, PendingSignInStoreInMemory>();
        _ = services.AddSingleton<IAccessGate, AccessGateInMemory>();
        _ = services.AddSingleton<IAccountAudit, AccountAuditInMemory>();

        _ = services.AddSingleton(RestrictionKeySuppliers.None);
        _ = services.AddSingleton(Declared);
        _ = services.AddSingleton(ReservedUsernames.Default);
        _ = services.AddSingleton(addresses);

        _ = services.AddScoped<SmsBalance>();
        _ = services.AddScoped<SendingService>();
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
        _ = services.AddScoped<StepUpGuard>();
        _ = services.AddScoped<SessionService>();
        _ = services.AddScoped<ISessions>(provider => provider.GetRequiredService<SessionService>());
        _ = services.AddScoped<PreAuthenticationService>();
        _ = services.AddScoped<RegistrationService>();
        _ = services.AddScoped<IRegistration>(provider =>
            provider.GetRequiredService<RegistrationService>());
        _ = services.AddScoped<IdentifierService>();
        _ = services.AddScoped<IIdentifiers>(provider =>
            provider.GetRequiredService<IdentifierService>());
        _ = services.AddScoped<AccountService>();
        _ = services.AddScoped<IAccount>(provider => provider.GetRequiredService<AccountService>());
        _ = services.AddScoped<TotpService>();
        _ = services.AddScoped<WebAuthnService>();
        _ = services.AddScoped<SignInLinks>();
        _ = services.AddScoped<AuthenticationService>();
        _ = services.AddScoped<IAuthentication>(provider =>
            provider.GetRequiredService<AuthenticationService>());

        _ = services.AddSingleton(new BrowserSessionCookies(application));
        _ = services.AddScoped<SynchronizerTokens>();
        _ = services.AddScoped<ResourceIsolation>();
        _ = services.AddScoped<CustomRequestHeader>();
        _ = services.AddScoped<OriginValidation>();
        _ = services.AddScoped<RequestSession>();
        _ = services.AddScoped<SessionResolution>();
        _ = services.AddScoped<FirstContact>();
        _ = services.AddScoped<SynchronizerToken>();

        _ = services.ConfigureHttpJsonOptions(options =>
        {
            // The contexts spell an enum as the contract spells it, and a request is
            // read through these options rather than through a context, so the same
            // converter stands here (API-CONV-002).
            options.SerializerOptions.Converters.Add(new JsonStringEnumConverter<IdentifierKind>());
            options.SerializerOptions.Converters.Add(new JsonStringEnumConverter<Factor>());
            options.SerializerOptions.TypeInfoResolverChain.Add(RegistrationJson.Default);
            options.SerializerOptions.TypeInfoResolverChain.Add(AuthenticationJson.Default);
            options.SerializerOptions.TypeInfoResolverChain.Add(AccountJson.Default);
            options.SerializerOptions.TypeInfoResolverChain.Add(WellKnownJson.Default);
        });
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await _application.DisposeAsync();
        await Work.DisposeAsync();

        _randomness.Dispose();
    }
}
