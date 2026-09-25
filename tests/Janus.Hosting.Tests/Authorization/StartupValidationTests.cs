using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Janus.Authentication.Tests;
using Janus.Authentication.Tests.Accounts;
using Janus.Authentication.Tests.Mailboxes;
using Janus.Authentication.Tests.Sending;
using Janus.Core;
using Janus.Core.Configuration;
using Janus.Hosting.Bff;
using Janus.Hosting.Sending;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;
using Xunit;

namespace Janus.Hosting.Tests.Authorization;

/// <summary>
/// What the checks that read the database decide over a deployment, and where they run
/// (AUTHZ-MODEL-004, AUTHZ-DERIVE-004), and what they warn of (INT-MAIL-011).
/// </summary>
/// <param name="host">The deployment the checks read.</param>
[Trait("kind", "integration")]
public sealed class StartupValidationTests(HostFixture host) : IClassFixture<HostFixture>
{
    private static readonly string Showing =
        Settings.OrganizationPhoto.For("2f8d4c1e-0000-7000-8000-000000000001").ToString();

    private static readonly string Forgotten =
        "DELETE FROM identity.settings WHERE key = '" + Showing + "';";

    private static readonly string Defaulting =
        "INSERT INTO identity.settings (key, value) VALUES ('"
        + Settings.RedirectDefaultClient.Key
        + "', 'nobody');";

    private const string Unmigrated = "behind";

    private static readonly string Declaring =
        "INSERT INTO identity.settings (key, value) VALUES ('"
        + Settings.NotificationEmailRelayRegistered.Key
        + "', '[\"mail.example.test\"]');";

    private static readonly string Undeclaring =
        "DELETE FROM identity.settings WHERE key = '"
        + Settings.NotificationEmailRelayRegistered.Key
        + "';";

    private readonly EventsInMemory _events = new();

    private static readonly string Undefaulted =
        "DELETE FROM identity.settings WHERE key = '"
        + Settings.RedirectDefaultClient.Key
        + "';";


    /// <summary>
    /// AUTHZ-MODEL-004, AUTHZ-DERIVE-004 AC1: the deployment's roles allow what it
    /// declares and the columns its derivation names are indexed, so it starts.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task StartAsync_TheDeploymentAsItStands_StartsAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        using IHost deployment = Deployed();

        await deployment.StartAsync(cancellationToken);
        await deployment.StopAsync(cancellationToken);
    }

    /// <summary>
    /// AUTHZ-MODEL-004 AC1, AC2: a role written into the deployment allowing a
    /// permission the model does not declare stops the deployment as it starts, with
    /// its own code, which the declaration alone could never decide.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task AUTHZ_MODEL_004_AC1_AStoredRoleAllowingAnUndeclaredPermissionIsRefusedAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await WriteAsync(
            """
            INSERT INTO identity.roles (name) VALUES ('forger');
            INSERT INTO identity.role_permissions (role, permission)
            VALUES ('forger', 'document:forge');
            """,
            cancellationToken);

        try
        {
            using IHost deployment = Deployed();

            StartupException refused = await Assert.ThrowsAsync<StartupException>(
                async () => await deployment.StartAsync(cancellationToken));

            Assert.Equal(ErrorCodes.StartupUndeclaredPermission, refused.Failure?.Code);
        }
        finally
        {
            await WriteAsync("DELETE FROM identity.roles WHERE name = 'forger';", cancellationToken);
        }
    }

    /// <summary>
    /// AUTH-ABUSE-005 AC3 and LIB-EXT-001: a deployment that has declared no message
    /// catalogue answers out of the one the library ships, in the language it declared,
    /// and starts. What the check refuses is a language with no words, not the absence
    /// of a catalogue of the deployment's own.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task AUTH_ABUSE_005_AC3_ADeploymentThatDeclaredNoMessagesStartsOnTheShippedOnesAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        using IHost deployment = Deployed(catalogue: false);

        await deployment.StartAsync(cancellationToken);

        _ = Assert.IsType<DefaultMessageTemplates>(
            deployment.Services.GetRequiredService<IMessageTemplates>());

        await deployment.StopAsync(cancellationToken);
    }

    /// <summary>
    /// PRIV-RIGHT-005b AC3: the deployment declares its documents sensitive, so a
    /// deployment that registered nothing to do the host-side work for them is
    /// stopped as it starts rather than at the erasure that would reach no one.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task PRIV_RIGHT_005b_AC3_ADeploymentWithNoHandlerForItsSensitiveTypeIsRefusedAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        using IHost deployment = Deployed(handlers: false);

        StartupException refused = await Assert.ThrowsAsync<StartupException>(
            async () => await deployment.StartAsync(cancellationToken));

        Assert.Equal(ErrorCodes.StartupDeclarationMissing, refused.Failure?.Code);
        Assert.Equal("document", refused.Failure?.Details["handler"].GetString());
    }

    /// <summary>
    /// LIB-HOST-001, REG-PM-001: the frontend's pages are a declaration with no
    /// default, so a deployment that registered none is stopped as it starts rather
    /// than answering a password manager as a site that offers neither page.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task REG_PM_001_ADeploymentThatDeclaredNoPasskeyPagesIsRefusedAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        using IHost deployment = Deployed(addresses: false);

        StartupException refused = await Assert.ThrowsAsync<StartupException>(
            async () => await deployment.StartAsync(cancellationToken));

        Assert.Equal(ErrorCodes.StartupDeclarationMissing, refused.Failure?.Code);
        Assert.Equal("passkeyAddresses", refused.Failure?.Details["key"].GetString());
    }

    /// <summary>
    /// LIB-HOST-001, AUTH-SESS-012 AC3: where a browser holding no session is sent is
    /// likewise a declaration with no default, so a deployment that registered none is
    /// stopped as it starts rather than meeting an interactive authorization request
    /// with nowhere to forward it.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task AUTH_SESS_012_AC3_ADeploymentThatDeclaredNoSignInScreenIsRefusedAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        using IHost deployment = Deployed(signIn: false);

        StartupException refused = await Assert.ThrowsAsync<StartupException>(
            async () => await deployment.StartAsync(cancellationToken));

        Assert.Equal(ErrorCodes.StartupDeclarationMissing, refused.Failure?.Code);
        Assert.Equal("authenticationAddresses.signIn", refused.Failure?.Details["key"].GetString());
    }

    /// <summary>
    /// BFF-SESS-006, LIB-HOST-001, D-162: which client of the provider an application
    /// is has no default either, so a deployment that declared none is stopped as it
    /// starts rather than at the first person who arrives holding no session.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task BFF_SESS_006_ADeploymentThatDeclaredNoSignOnClientIsRefusedAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        using IHost deployment = Deployed(client: false);

        StartupException refused = await Assert.ThrowsAsync<StartupException>(
            async () => await deployment.StartAsync(cancellationToken));

        Assert.Equal(ErrorCodes.StartupDeclarationMissing, refused.Failure?.Code);
        Assert.Equal("signOnClient.clientId", refused.Failure?.Details["key"].GetString());
    }

    /// <summary>
    /// IDN-ATTR-002, LIB-HOST-001: the library reads no image, so a deployment whose
    /// policy shows photos and which declared no codec is stopped as it starts rather
    /// than meeting the first upload with nothing to read it.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task IDN_ATTR_002_ADeploymentThatShowsPhotosWithNoCodecIsRefusedAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await ShowingPhotosAsync(cancellationToken);

        try
        {
            using IHost deployment = Deployed();

            StartupException refused = await Assert.ThrowsAsync<StartupException>(
                async () => await deployment.StartAsync(cancellationToken));

            Assert.Equal(ErrorCodes.StartupDeclarationMissing, refused.Failure?.Code);
            Assert.Equal("imageCodec", refused.Failure?.Details["key"].GetString());
        }
        finally
        {
            await WriteAsync(Forgotten, cancellationToken);
        }
    }

    /// <summary>
    /// IDN-ATTR-002, LIB-HOST-001: the same deployment starts once it declares the
    /// codec, which is the whole of what the check asks of it.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task IDN_ATTR_002_ADeploymentThatShowsPhotosAndDeclaredACodecStartsAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await ShowingPhotosAsync(cancellationToken);

        try
        {
            using IHost deployment = Deployed(codec: true);

            await deployment.StartAsync(cancellationToken);
            await deployment.StopAsync(cancellationToken);
        }
        finally
        {
            await WriteAsync(Forgotten, cancellationToken);
        }
    }

    /// <summary>
    /// INT-MAIL-010, LIB-HOST-001: the app passwords of a hosted mailbox are reached
    /// with a token issued to the mail server's client, so a deployment that registers a
    /// mail server and declares no client for it is stopped as it starts, and the same
    /// deployment starts once it declares one.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task INT_MAIL_010_ADeploymentHostingMailDeclaresTheMailServersClientAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        using (IHost undeclared = Deployed(mail: true))
        {
            StartupException refused = await Assert.ThrowsAsync<StartupException>(
                async () => await undeclared.StartAsync(cancellationToken));

            Assert.Equal(ErrorCodes.StartupDeclarationMissing, refused.Failure?.Code);
            Assert.Equal("mailServerClient.clientId", refused.Failure?.Details["key"].GetString());
        }

        using IHost declared = Deployed(mail: true, mailClient: true);

        await declared.StartAsync(cancellationToken);
        await declared.StopAsync(cancellationToken);
    }

    /// <summary>
    /// IDN-LIFE-012a, LIB-HOST-001: a social provider is optional, and one declared is
    /// declared whole, so a declaration that could verify none of its events stops the
    /// deployment as it starts, naming the part that does not hold; one declared whole
    /// starts.
    /// </summary>
    /// <param name="part">The part that does not hold.</param>
    /// <returns>The work of running it.</returns>
    [Theory]
    [InlineData("provider")]
    [InlineData("metadata")]
    [InlineData("clientIds")]
    public async Task IDN_LIFE_012a_ASocialProviderDeclaredShortOfWholeIsRefusedAsync(string part)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        var metadata = new Uri("https://accounts.google.test/.well-known/risc-configuration");
        var whole = new SocialProvider(Factor.Google, metadata, ["the-client"]);
        SocialProvider[] declared = part switch
        {
            "provider" => [whole, whole with { Provider = Factor.Password }],
            "metadata" => [whole with { Metadata = new Uri("http://accounts.google.test/risc") }],
            _ => [whole with { ClientIds = [] }],
        };

        using (IHost refusedHost = Deployed(providers: declared))
        {
            StartupException refused = await Assert.ThrowsAsync<StartupException>(
                async () => await refusedHost.StartAsync(cancellationToken));

            Assert.Equal(ErrorCodes.StartupDeclarationMissing, refused.Failure?.Code);
            Assert.Equal("socialProvider." + part, refused.Failure?.Details["key"].GetString());
        }

        using IHost started = Deployed(providers: [whole]);

        await started.StartAsync(cancellationToken);
        await started.StopAsync(cancellationToken);
    }

    /// <summary>
    /// API-REDIR-001: the default a destination falls back to is read against the
    /// registry as the deployment starts, so a key naming a client nothing registered
    /// stops it there rather than at the registration that would resolve to nothing.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task API_REDIR_001_ADeploymentNamingADefaultClientTheRegistryLacksIsRefusedAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await WriteAsync(Defaulting, cancellationToken);

        try
        {
            using IHost deployment = Deployed();

            StartupException refused = await Assert.ThrowsAsync<StartupException>(
                async () => await deployment.StartAsync(cancellationToken));

            Assert.Equal(ErrorCodes.StartupRedirectClient, refused.Failure?.Code);
            Assert.Equal(
                Settings.RedirectDefaultClient.Key.ToString(),
                refused.Failure?.Details["key"].GetString());
        }
        finally
        {
            await WriteAsync(Undefaulted, cancellationToken);
        }
    }

    /// <summary>
    /// AUTHZ-MODEL-004 AC2: the web server is a hosted service of the host's, and
    /// hosted services start in the order they were registered, so the checks that read
    /// the database stand at the head of the collection and no request is served
    /// before them (D-160).
    /// </summary>
    [Fact]
    public void AUTHZ_MODEL_004_AC2_TheChecksStartBeforeEverythingElseRegistered()
    {
        var services = new ServiceCollection();

        services.AddSingleton<IHostedService>(new ServerStandIn());

        Declared(services);

        ServiceDescriptor first = services.First(
            service => service.ServiceType == typeof(IHostedService));

        Assert.Equal(0, services.IndexOf(first));
        Assert.Equal(typeof(SchemaValidationService), first.ImplementationType);
    }

    /// <summary>
    /// OPS-MIG-002 AC1, AC2: a deployment pointed at a database the pipeline did not
    /// migrate is stopped as it starts, by name and before the web server registered
    /// after the library has served anything.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task OPS_MIG_002_AC1_ADeploymentOnAnUnmigratedDatabaseIsRefusedAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await WriteAsync(
            "CREATE DATABASE " + Unmigrated + " TEMPLATE template0 "
                + "LOCALE_PROVIDER icu ICU_LOCALE 'und' LC_COLLATE 'C' LC_CTYPE 'C'",
            cancellationToken);

        var served = new ServerStandIn();
        using IHost deployment = new HostBuilder()
            .ConfigureServices(services => Declared(
                services.AddSingleton<IHostedService>(served),
                connection: new NpgsqlConnectionStringBuilder(host.ConnectionString)
                {
                    Database = Unmigrated,
                }.ConnectionString))
            .Build();

        StartupException refused = await Assert.ThrowsAsync<StartupException>(
            async () => await deployment.StartAsync(cancellationToken));

        Assert.Equal(ErrorCodes.StartupSchemaMismatch, refused.Failure?.Code);
        Assert.NotEqual(0, refused.Failure?.Details["pending"].GetArrayLength());
        Assert.False(served.Started);
    }

    /// <summary>
    /// INT-MAIL-011 AC1: a deployment in which Continue with Apple is a way in and whose
    /// sending domain is not declared as registered with the relay starts, and warns as
    /// it does, naming the domain; declaring it quiets the warning.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task INT_MAIL_011_AC1_AnUndeclaredSendingDomainWarnsAsTheDeploymentStartsAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        using (IHost undeclared = Deployed())
        {
            await undeclared.StartAsync(cancellationToken);
            await undeclared.StopAsync(cancellationToken);
        }

        AlertRaised raised = Assert.Single(_events.Of<AlertRaised>());

        Assert.Equal(AlertCondition.RelayDomainUnregistered, raised.Condition);
        Assert.Equal(AlertSeverity.Normal, raised.Severity);
        Assert.Equal("mail.example.test", raised.Details["domain"].GetString());

        await WriteAsync(Declaring, cancellationToken);

        try
        {
            using IHost declared = Deployed();

            await declared.StartAsync(cancellationToken);
            await declared.StopAsync(cancellationToken);

            Assert.Single(_events.Published);
        }
        finally
        {
            await WriteAsync(Undeclaring, cancellationToken);
        }
    }

    /// <summary>
    /// LIB-HOST-001 AC1, AC3: the deployment names the keys the list gives and
    /// retention for the categories it declares, and nothing else, and it starts.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task LIB_HOST_001_AC3_ADeploymentNamingOnlyTheListedKeysStartsAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        var served = new ServerStandIn();
        using IHost deployment = new HostBuilder()
            .ConfigureServices(services => Declared(services.AddSingleton<IHostedService>(served)))
            .Build();

        await deployment.StartAsync(cancellationToken);
        await deployment.StopAsync(cancellationToken);

        Assert.True(served.Started);
    }

    /// <summary>
    /// LIB-HOST-001 AC4: a deployment that never named the language its legal
    /// documents bind in is stopped as it starts, under that key's own code.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task LIB_HOST_001_AC4_ADeploymentWithoutItsGoverningLanguageIsRefusedAsync()
    {
        StartupException refused = await RefusedWithoutAsync(Settings.LegalGoverningLanguage.Key, "ar");

        Assert.Equal(ErrorCodes.StartupGoverningLanguage, refused.Failure?.Code);
        Assert.Equal("legal.governinglanguage", refused.Failure?.Details["key"].GetString());
    }

    /// <summary>
    /// LIB-HOST-001 AC2: a key the deployment has to name and left unnamed stops it as
    /// it starts, by the key's name and before the web server registered after the
    /// library has served anything.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task LIB_HOST_001_AC2_AnUnnamedKeyIsRefusedByNameBeforeTheServerStartsAsync()
    {
        StartupException refused = await RefusedWithoutAsync(
            Settings.HostingEnvironment.Key,
            "a rented virtual machine");

        Assert.Equal(ErrorCodes.StartupDeclarationMissing, refused.Failure?.Code);
        Assert.Equal("hosting.environment", refused.Failure?.Details["key"].GetString());
    }

    /// <summary>
    /// LIB-HOST-001 AC2: a conditional key is one the deployment has to name once its
    /// condition holds, so a deployment that moves its data outside Egypt and names no
    /// basis for the transfer is stopped by that key.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task LIB_HOST_001_AC2_AConditionalKeyIsRefusedOnceItsConditionHoldsAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await WriteAsync(Located(HostingLocation.Outside), cancellationToken);

        try
        {
            using IHost deployment = Deployed();

            StartupException refused = await Assert.ThrowsAsync<StartupException>(
                async () => await deployment.StartAsync(cancellationToken));

            Assert.Equal(ErrorCodes.StartupDeclarationMissing, refused.Failure?.Code);
            Assert.Equal("hosting.crossborderbasis", refused.Failure?.Details["key"].GetString());
        }
        finally
        {
            await WriteAsync(Located(HostingLocation.Inside), cancellationToken);
        }
    }

    // LIB-HOST-001: the deployment started with one key it has to name left unnamed,
    // which is named again afterwards whatever the start did.
    private async Task<StartupException> RefusedWithoutAsync(ConfigurationKey key, string value)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await WriteAsync("DELETE FROM identity.settings WHERE key = '" + key + "';", cancellationToken);

        try
        {
            var served = new ServerStandIn();
            using IHost deployment = new HostBuilder()
                .ConfigureServices(services => Declared(services.AddSingleton<IHostedService>(served)))
                .Build();

            StartupException refused = await Assert.ThrowsAsync<StartupException>(
                async () => await deployment.StartAsync(cancellationToken));

            Assert.False(served.Started);

            return refused;
        }
        finally
        {
            await WriteAsync(
                "INSERT INTO identity.settings (key, value) VALUES ('" + key + "', '" + value + "');",
                cancellationToken);
        }
    }

    private static string Located(HostingLocation location) =>
        "UPDATE identity.settings SET value = '"
        + Settings.HostingLocation.Write(location)
        + "' WHERE key = '"
        + Settings.HostingLocation.Key
        + "';";

    private IHost Deployed(
        bool catalogue = true,
        bool handlers = true,
        bool addresses = true,
        bool signIn = true,
        bool client = true,
        bool codec = false,
        bool mail = false,
        bool mailClient = false,
        IReadOnlyList<SocialProvider>? providers = null) =>
        new HostBuilder()
            .ConfigureServices(services => Declared(
                services,
                catalogue,
                handlers,
                addresses,
                signIn,
                client,
                codec,
                mail,
                mailClient,
                providers: providers))
            .Build();

    // The library registered over this deployment, as the host's own code registers
    // it, with the messages the deployment has written (LIB-HOST-001).
    private IServiceCollection Declared(
        IServiceCollection services,
        bool catalogue = true,
        bool handlers = true,
        bool addresses = true,
        bool signIn = true,
        bool client = true,
        bool codec = false,
        bool mail = false,
        bool mailClient = false,
        string? connection = null,
        IReadOnlyList<SocialProvider>? providers = null)
    {
        // Where what the library announces goes, the host's own (LIB-HOST-001).
        services.AddSingleton<IEvents>(_events);

        if (codec)
        {
            services.AddSingleton(new ImageCodecInMemory().Declared);
        }

        if (mail)
        {
            services.AddSingleton<IMailServer>(new MailServerInMemory());
        }

        if (mailClient)
        {
            services.AddSingleton(new MailServerClient("mail-server"));
        }

        if (catalogue)
        {
            services.AddSingleton<IMessageTemplates>(new MessageTemplatesInMemory());
        }

        if (handlers)
        {
            services.AddSingleton<ISubjectEventSubscriber>(new HostSubjectEvents());
        }

        if (addresses)
        {
            services.AddSingleton(new PasskeyAddresses(
                "https://accounts.example.test/password",
                "https://accounts.example.test/passkeys/new",
                "https://accounts.example.test/passkeys"));
        }

        if (signIn)
        {
            services.AddSingleton(new AuthenticationAddresses(
                "https://accounts.example.test/signin",
                "https://accounts.example.test"));
        }

        if (client)
        {
            services.AddSingleton(new SignOnClient("this-application"));
        }

        foreach (SocialProvider provider in providers ?? [])
        {
            services.AddSingleton(provider);
        }

        return services.AddJanus(
            connection ?? host.ConnectionString,
            new KeyEncryptionKeys(1, new Dictionary<int, ReadOnlyMemory<byte>> { [1] = new byte[32] }),
            new byte[32],
            Encoding.UTF8.GetBytes("the secret this application presents"),
            HostFixture.Declaration(),
            ApplicationKind.Public);
    }

    // IDN-ATTR-002: an organization shows photos by its key, which is a settings row
    // and nothing the declaration can carry.
    private async Task ShowingPhotosAsync(CancellationToken cancellationToken) =>
        await WriteAsync(
            "INSERT INTO identity.settings (key, value) VALUES ('" + Showing + "', 'true');",
            cancellationToken);

    private async Task WriteAsync(string statement, CancellationToken cancellationToken)
    {
        await using NpgsqlConnection connection = await host.OpenAsync();

        await connection.ExecuteAsync(new CommandDefinition(
            statement,
            cancellationToken: cancellationToken));
    }

    // A hosted service of the host's own, registered before the library is, standing
    // for the web server the deployment starts.
    private sealed class ServerStandIn : IHostedService
    {
        public bool Started { get; private set; }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            Started = true;

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
