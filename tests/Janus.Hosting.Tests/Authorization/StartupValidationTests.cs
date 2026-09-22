using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Janus.Authentication.Tests.Accounts;
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
/// (AUTHZ-MODEL-004, AUTHZ-DERIVE-004).
/// </summary>
/// <param name="host">The deployment the checks read.</param>
[Trait("kind", "integration")]
public sealed class StartupValidationTests(HostFixture host) : IClassFixture<HostFixture>
{
    private static readonly string Showing =
        Settings.OrganizationPhoto.For("2f8d4c1e-0000-7000-8000-000000000001").ToString();

    private static readonly string Forgotten =
        "DELETE FROM janus.settings WHERE key = '" + Showing + "';";

    private static readonly string Defaulting =
        "INSERT INTO janus.settings (key, value) VALUES ('"
        + Settings.RedirectDefaultClient.Key
        + "', 'nobody');";

    private static readonly string Undefaulted =
        "DELETE FROM janus.settings WHERE key = '"
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
            INSERT INTO janus.roles (name) VALUES ('forger');
            INSERT INTO janus.role_permissions (role, permission)
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
            await WriteAsync("DELETE FROM janus.roles WHERE name = 'forger';", cancellationToken);
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
        Assert.Equal(typeof(ModelValidationService), first.ImplementationType);
    }

    private IHost Deployed(
        bool catalogue = true,
        bool handlers = true,
        bool addresses = true,
        bool signIn = true,
        bool codec = false) =>
        new HostBuilder()
            .ConfigureServices(services => Declared(services, catalogue, handlers, addresses, signIn, codec))
            .Build();

    // The library registered over this deployment, as the host's own code registers
    // it, with the messages the deployment has written (LIB-HOST-001).
    private IServiceCollection Declared(
        IServiceCollection services,
        bool catalogue = true,
        bool handlers = true,
        bool addresses = true,
        bool signIn = true,
        bool codec = false)
    {
        if (codec)
        {
            services.AddSingleton(new ImageCodecInMemory().Declared);
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
            services.AddSingleton(new AuthenticationAddresses("https://accounts.example.test/signin"));
        }

        return services.AddJanus(
            host.ConnectionString,
            new KeyEncryptionKeys(1, new Dictionary<int, ReadOnlyMemory<byte>> { [1] = new byte[32] }),
            new byte[32],
            HostFixture.Declaration(),
            JanusApplication.Public);
    }

    // IDN-ATTR-002: an organization shows photos by its key, which is a settings row
    // and nothing the declaration can carry.
    private async Task ShowingPhotosAsync(CancellationToken cancellationToken) =>
        await WriteAsync(
            "INSERT INTO janus.settings (key, value) VALUES ('" + Showing + "', 'true');",
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
        public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
