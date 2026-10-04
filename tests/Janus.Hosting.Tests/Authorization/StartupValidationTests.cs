using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Janus.Authentication.Oidc;
using Janus.Authentication.Tests;
using Janus.Authentication.Tests.Accounts;
using Janus.Authentication.Tests.Mailboxes;
using Janus.Authentication.Tests.Sending;
using Janus.Core;
using Janus.Core.Configuration;
using Janus.Hosting.Bff;
using Janus.Hosting.Sending;
using Microsoft.EntityFrameworkCore;
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
public sealed class StartupValidationTests(HostFixture host) : IClassFixture<HostFixture>, IAsyncLifetime
{
    // API-LAND-001, LIB-HOST-001: the two applications a link lands on, each the origin
    // of a browser client the deployment registered, the authentication one where the
    // sign-in address is.
    private static readonly LandingOrigins Landing = new(
        "https://accounts.example.test",
        "https://account.example.test");

    private static readonly OidcClient[] Browsers =
    [
        new("accounts-application", "Accounts", OidcClientKind.BrowserApplication, "https://accounts.example.test/return", ["openid"]),
        new("account-application", "Account", OidcClientKind.BrowserApplication, "https://account.example.test/return", ["openid"]),
        new("elsewhere-service", "Elsewhere", OidcClientKind.Protocol, "https://elsewhere.example.test/return", ["openid"]),
    ];

    private static readonly string Showing =
        Settings.OrganizationPolicy.For("2f8d4c1e-0000-7000-8000-000000000001").ToString();

    private static readonly string Forgotten =
        "DELETE FROM identity.settings WHERE key = '" + Showing + "';";

    private static readonly string Locked =
        Settings.OrganizationPolicy.For("2f8d4c1e-0000-7000-8000-000000000002").ToString();

    private static readonly string Unlocked =
        "DELETE FROM identity.settings WHERE key = '" + Locked + "';";

    private static readonly string Defaulting =
        "INSERT INTO identity.settings (key, value) VALUES ('"
        + Settings.RedirectDefaultClient.Key
        + "', 'nobody');";

    private const string Unmigrated = "behind";

    private const string Unserved = "unserved";

    private static readonly string Declaring =
        "INSERT INTO identity.settings (key, value) VALUES ('"
        + Settings.NotificationEmailRelayRegistered.Key
        + "', '[\"mail.example.test\"]');";

    private static readonly string Undeclaring =
        "DELETE FROM identity.settings WHERE key = '"
        + Settings.NotificationEmailRelayRegistered.Key
        + "';";

    private static readonly string Undefaulted =
        "DELETE FROM identity.settings WHERE key = '"
        + Settings.RedirectDefaultClient.Key
        + "';";

    // IDN-LIFE-012a: a provider declared whole, and the static secret the host's secret
    // source answers for it.
    private static readonly SocialProvider Google = new(
        Factor.Google,
        new Uri("https://accounts.google.test/.well-known/risc-configuration"),
        ["the-client"],
        new Uri("https://accounts.google.test/.well-known/openid-configuration"),
        new Uri("https://identity.example.test/callbacks/providers/google/return"));

    private static readonly IReadOnlyDictionary<string, ProviderCredential> GoogleSecret =
        new Dictionary<string, ProviderCredential>(StringComparer.Ordinal)
        {
            ["google"] = ProviderCredential.Secret("the-client-secret"u8.ToArray()),
        };


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
    /// OPS-DB-003 AC3, AUTHZ-DERIVE-004 AC1: what the check reads is the database's own
    /// catalogue, so with the index on the reviewer column dropped from the host's
    /// table, the deployment whose derivation names that column is stopped as it
    /// starts, naming the relationship.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task OPS_DB_003_AC3_ADerivationColumnTheCatalogueFindsUnindexedIsRefusedAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await WriteAsync("DROP INDEX host.ix_reviewers_reviewer;", cancellationToken);

        try
        {
            using IHost deployment = Deployed();

            StartupException refused = await Assert.ThrowsAsync<StartupException>(
                async () => await deployment.StartAsync(cancellationToken));

            Assert.Equal(ErrorCodes.StartupUnindexedDerivation, refused.Failure?.Code);
            Assert.Equal("reviewer", refused.Failure?.Details["relationship"].GetString());
        }
        finally
        {
            await WriteAsync(
                "CREATE INDEX ix_reviewers_reviewer ON host.reviewers (reviewer);",
                cancellationToken);
        }
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
    /// DR-016 AC2, IDN-LIFE-003a: a host subscriber registered under the name the
    /// erasure ledger confirms under would read the ledger's line as its own work, so
    /// the deployment is stopped as it starts.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task DR_016_AC2_ASubscriberUnderTheLedgersNameIsRefusedAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        using IHost deployment = new HostBuilder()
            .ConfigureServices(services =>
            {
                services.AddSingleton<ISubjectEventSubscriber>(new Impostor());
                Declared(services);
            })
            .Build();

        StartupException refused = await Assert.ThrowsAsync<StartupException>(
            async () => await deployment.StartAsync(cancellationToken));

        Assert.Equal(ErrorCodes.StartupSubscriberName, refused.Failure?.Code);
        Assert.Equal("erasure-ledger", refused.Failure?.Details["handler"].GetString());
    }

    /// <summary>
    /// INT-SMS-003 AC3: a subscriber's name fills the <c>outstanding</c> place, so one
    /// registered under a name outside the rule stops the deployment as it starts,
    /// naming it and its member; one inside the rule starts.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task INT_SMS_003_AC3_ASubscriberNamedOutsideTheRuleIsRefusedAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        using (IHost refusedHost = new HostBuilder()
            .ConfigureServices(services =>
            {
                services.AddSingleton<ISubjectEventSubscriber>(new Named("Host Events"));
                Declared(services);
            })
            .Build())
        {
            StartupException refused = await Assert.ThrowsAsync<StartupException>(
                async () => await refusedHost.StartAsync(cancellationToken));

            Assert.Equal(ErrorCodes.StartupDeclarationInvalid, refused.Failure?.Code);
            Assert.Equal("Host Events", refused.Failure?.Details["declaration"].GetString());
            Assert.Equal("name", refused.Failure?.Details["field"].GetString());
        }

        using IHost started = new HostBuilder()
            .ConfigureServices(services =>
            {
                services.AddSingleton<ISubjectEventSubscriber>(new Named("host.events_2"));
                Declared(services);
            })
            .Build();

        await started.StartAsync(cancellationToken);
        await started.StopAsync(cancellationToken);
    }

    /// <summary>
    /// INT-SMS-003 AC3: a governing document's name fills the <c>document</c> place, so
    /// a purpose that names one outside the rule stops the deployment as it starts,
    /// naming the purpose and its member; one inside the rule starts.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task INT_SMS_003_AC3_AGoverningDocumentNamedOutsideTheRuleIsRefusedAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        using (IHost refusedHost = Deployed(document: "Recommendation Terms"))
        {
            StartupException refused = await Assert.ThrowsAsync<StartupException>(
                async () => await refusedHost.StartAsync(cancellationToken));

            Assert.Equal(ErrorCodes.StartupDeclarationInvalid, refused.Failure?.Code);
            Assert.Equal("recommendations", refused.Failure?.Details["declaration"].GetString());
            Assert.Equal("document", refused.Failure?.Details["field"].GetString());
        }

        using IHost started = Deployed(document: "recommendation-terms");

        await started.StartAsync(cancellationToken);
        await started.StopAsync(cancellationToken);
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
    /// REG-PM-001 AC3, LIB-HOST-001: a page declared as white space is no page, so a
    /// field left blank stops the deployment as it starts, named, as an empty one does.
    /// </summary>
    /// <param name="field">The field left blank, as the refusal names it.</param>
    /// <returns>The work of running it.</returns>
    [Theory]
    [InlineData("passkeyAddresses.changePassword")]
    [InlineData("passkeyAddresses.enrol")]
    [InlineData("passkeyAddresses.manage")]
    [InlineData("authenticationAddresses.signIn")]
    [InlineData("authenticationAddresses.provider")]
    public async Task REG_PM_001_AC3_ABlankFieldIsRefusedAsync(string field)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        using IHost deployment = new HostBuilder()
            .ConfigureServices(services =>
            {
                services.AddSingleton(new PasskeyAddresses(
                    field is "passkeyAddresses.changePassword" ? " \t" : "https://accounts.example.test/password",
                    field is "passkeyAddresses.enrol" ? " \t" : "https://accounts.example.test/passkeys/new",
                    field is "passkeyAddresses.manage" ? " \t" : "https://accounts.example.test/passkeys"));
                services.AddSingleton(new AuthenticationAddresses(
                    field is "authenticationAddresses.signIn" ? " \t" : "https://accounts.example.test/signin",
                    field is "authenticationAddresses.provider" ? " \t" : "https://accounts.example.test"));
                Declared(services, addresses: false, signIn: false);
            })
            .Build();

        StartupException refused = await Assert.ThrowsAsync<StartupException>(
            async () => await deployment.StartAsync(cancellationToken));

        Assert.Equal(ErrorCodes.StartupDeclarationMissing, refused.Failure?.Code);
        Assert.Equal(field, refused.Failure?.Details["key"].GetString());
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
    /// LIB-HOST-001, API-LAND-001: where a link lands has no default, so a deployment
    /// that declared no landing origins is stopped as it starts, naming the
    /// authentication application's.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task LIB_HOST_001_ADeploymentThatDeclaredNoLandingOriginsIsRefusedAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        using IHost deployment = Deployed(landed: false);

        StartupException refused = await Assert.ThrowsAsync<StartupException>(
            async () => await deployment.StartAsync(cancellationToken));

        Assert.Equal(ErrorCodes.StartupDeclarationMissing, refused.Failure?.Code);
        Assert.Equal("landingOrigins.authentication", refused.Failure?.Details["key"].GetString());
    }

    /// <summary>
    /// LIB-HOST-001 AC6, API-LAND-001: a landing origin no registered browser client
    /// returns to, a protocol client's included, is no application of this deployment,
    /// and one that is not an https origin is none at all, so either stops it as it
    /// starts, naming the origin.
    /// </summary>
    /// <param name="account">The account application's origin as declared.</param>
    /// <returns>The work of running it.</returns>
    [Theory]
    [InlineData("https://unregistered.example.test")]
    [InlineData("https://elsewhere.example.test")]
    [InlineData("https://account.example.test/")]
    [InlineData("http://account.example.test")]
    public async Task LIB_HOST_001_AC6_ALandingOriginNoBrowserClientReturnsToIsRefusedAsync(string account)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        using IHost deployment = Deployed(landing: Landing with { Account = account });

        StartupException refused = await Assert.ThrowsAsync<StartupException>(
            async () => await deployment.StartAsync(cancellationToken));

        Assert.Equal(ErrorCodes.StartupDeclarationInvalid, refused.Failure?.Code);
        Assert.Equal("landingOrigins.account", refused.Failure?.Details["key"].GetString());
    }

    /// <summary>
    /// LIB-HOST-001 AC6: the authentication application is where the sign-in address
    /// is, so an authentication origin elsewhere is refused even where a browser client
    /// returns to it.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task LIB_HOST_001_AC6_AnAuthenticationOriginThatIsNotTheSignInOriginIsRefusedAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        using IHost deployment = Deployed(landing: Landing with { Authentication = Landing.Account });

        StartupException refused = await Assert.ThrowsAsync<StartupException>(
            async () => await deployment.StartAsync(cancellationToken));

        Assert.Equal(ErrorCodes.StartupDeclarationInvalid, refused.Failure?.Code);
        Assert.Equal("landingOrigins.authentication", refused.Failure?.Details["key"].GetString());
    }

    /// <inheritdoc/>
    public async ValueTask InitializeAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        using IHost deployment = Deployed();

        // CONV-DESIGN-007: the key ring is filled before a client's secret is wrapped,
        // which is what its hosted service does as the deployment starts.
        KeyRingService ring = deployment.Services.GetServices<IHostedService>().OfType<KeyRingService>().Single();
        await ring.StartingAsync(cancellationToken);
        await ring.StartAsync(cancellationToken);

        await using AsyncServiceScope scope = deployment.Services.CreateAsyncScope();
        IOidcClientStore clients = scope.ServiceProvider.GetRequiredService<IOidcClientStore>();
        IUnitOfWork work = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        await work.BeginAsync(cancellationToken);

        foreach (OidcClient browser in Browsers)
        {
            if (await clients.FindAsync(browser.ClientId, cancellationToken) is null)
            {
                await clients.AddAsync(browser, RandomNumberGenerator.GetBytes(32), DateTimeOffset.UnixEpoch, cancellationToken);
            }
        }

        await work.CommitAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

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
    /// IDN-ATTR-002, OPS-CFG-003 AC4: the system policy is a stored policy like any
    /// organization's, so a deployment whose <c>policy.default</c> shows photos and which
    /// declared no codec is stopped as it starts.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task OPS_CFG_003_AC4_ADeploymentWhoseSystemPolicyShowsPhotosWithNoCodecIsRefusedAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await WriteAsync(
            "INSERT INTO identity.settings (key, value) VALUES ('" + Settings.PolicyDefault.Key + "', '"
                + """{"requiredAssurance":"aal1","loginFactors":["passkey","password"],"gates":{},"credentialRedundancy":"advisory","selfServiceRecovery":true,"emailDomains":[],"photos":true}"""
                + "');",
            cancellationToken);

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
            await WriteAsync(
                "DELETE FROM identity.settings WHERE key = '" + Settings.PolicyDefault.Key + "';",
                cancellationToken);
        }
    }

    /// <summary>
    /// IDN-ATTR-002 AC3, OPS-CFG-003: a deployment whose stored policies show no photo,
    /// as bootstrap leaves the administrative organization's, starts with no codec
    /// declared.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task IDN_ATTR_002_AC3_ADeploymentThatShowsNoPhotosStartsWithNoCodecAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await WriteAsync(
            "INSERT INTO identity.settings (key, value) VALUES ('" + Showing + "', '{\"photos\":false}');",
            cancellationToken);

        try
        {
            using IHost deployment = Deployed();

            await deployment.StartAsync(cancellationToken);
            await deployment.StopAsync(cancellationToken);
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
    /// REG-DOM-001 AC12 and X6 of D-166: a listed domain is verified and re-verified by
    /// its TXT record, so a deployment whose stored lock lists a domain and which
    /// registered no DNS resolver is stopped as it starts, and the same deployment
    /// starts once it registers one.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task REG_DOM_001_AC12_ADeploymentWhoseLockListsADomainNeedsAResolverAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await WriteAsync(
            "INSERT INTO identity.settings (key, value) VALUES ('" + Locked + "', '{\"emailDomains\":[\"example.test\"]}');",
            cancellationToken);

        try
        {
            using (IHost unresolved = Deployed())
            {
                StartupException refused = await Assert.ThrowsAsync<StartupException>(
                    async () => await unresolved.StartAsync(cancellationToken));

                Assert.Equal(ErrorCodes.StartupDeclarationMissing, refused.Failure?.Code);
                Assert.Equal("dnsResolver", refused.Failure?.Details["key"].GetString());
            }

            using IHost resolved = Deployed(resolver: true);

            await resolved.StartAsync(cancellationToken);
            await resolved.StopAsync(cancellationToken);
        }
        finally
        {
            await WriteAsync(Unlocked, cancellationToken);
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
    /// INT-MAIL-008 AC3 and LIB-EXT-001, D-166 (270): a deployment sends mail and the
    /// library ships no mail transport of its own, so one that registers none does not
    /// start, and the refusal names the transport.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task INT_MAIL_008_AC3_ADeploymentWithNoMailTransportDoesNotStartAsync()
    {
        using IHost deployment = Deployed(mailTransport: false);

        StartupException refused = await Assert.ThrowsAsync<StartupException>(
            async () => await deployment.StartAsync(TestContext.Current.CancellationToken));

        Assert.Equal(ErrorCodes.StartupDeclarationMissing, refused.Failure?.Code);
        Assert.Equal("mailTransport", refused.Failure?.Details["key"].GetString());
    }

    /// <summary>
    /// INT-SMS-006 AC2 and LIB-EXT-001, D-166 (270): every deployment sends text
    /// messages, its alerts among them, and the library ships no SMS transport of its
    /// own, so one that registers none does not start, and the refusal names the
    /// transport.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task INT_SMS_006_AC2_ADeploymentWithNoSmsTransportDoesNotStartAsync()
    {
        using IHost deployment = Deployed(smsTransport: false);

        StartupException refused = await Assert.ThrowsAsync<StartupException>(
            async () => await deployment.StartAsync(TestContext.Current.CancellationToken));

        Assert.Equal(ErrorCodes.StartupDeclarationMissing, refused.Failure?.Code);
        Assert.Equal("smsTransport", refused.Failure?.Details["key"].GetString());
    }

    /// <summary>
    /// LIB-HOST-001 AC2, IDN-LIFE-012a, D-175: a social provider is optional, and one
    /// declared is declared whole, so a declaration that is malformed stops the
    /// deployment as it starts, naming the provider as its credential is named and the
    /// member at fault; one declared whole starts.
    /// </summary>
    /// <param name="fault">What is wrong with the declaration.</param>
    /// <param name="declaration">The declaration the refusal names.</param>
    /// <param name="field">The member the refusal names.</param>
    /// <returns>The work of running it.</returns>
    [Theory]
    [InlineData("twice", "socialProvider.google", "provider")]
    [InlineData("notsocial", "socialProvider.password", "provider")]
    [InlineData("metadata", "socialProvider.google", "metadata")]
    [InlineData("configuration", "socialProvider.google", "configuration")]
    [InlineData("return", "socialProvider.google", "return")]
    [InlineData("clientIds", "socialProvider.google", "clientIds")]
    public async Task LIB_HOST_001_AC2_AMalformedSocialProviderIsRefusedNamingItAsync(
        string fault,
        string declaration,
        string field)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        SocialProvider whole = Google;
        SocialProvider[] declared = fault switch
        {
            "twice" => [whole, whole],
            "notsocial" => [whole with { Provider = Factor.Password }],
            "metadata" => [whole with { Metadata = new Uri("http://accounts.google.test/risc") }],
            "configuration" => [whole with { Configuration = new Uri("http://accounts.google.test/openid") }],
            "return" => [whole with { Return = new Uri("https://identity.example.test/callbacks/providers/apple/return") }],
            _ => [whole with { ClientIds = [] }],
        };
        var secrets = new SecretSourceInMemory(GoogleSecret);

        using (IHost refusedHost = Deployed(providers: declared, secrets: secrets))
        {
            StartupException refused = await Assert.ThrowsAsync<StartupException>(
                async () => await refusedHost.StartAsync(cancellationToken));

            Assert.Equal(ErrorCodes.StartupDeclarationInvalid, refused.Failure?.Code);
            Assert.Equal(declaration, refused.Failure?.Details["declaration"].GetString());
            Assert.Equal(field, refused.Failure?.Details["field"].GetString());
        }

        using IHost started = Deployed(providers: [whole], secrets: secrets);

        await started.StartAsync(cancellationToken);
        await started.StopAsync(cancellationToken);
    }

    /// <summary>
    /// LIB-HOST-001 AC2, OPS-SEC-001 AC2 and AUTH-KEY-002 AC2 (D-180): a deployment whose
    /// host declares no secret source does not start, and the refusal names the
    /// declaration, not a secret: it comes first in the start, before any secret is read,
    /// so a declared social provider whose credential would have been read changes
    /// nothing, and the web server never starts.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task LIB_HOST_001_AC2_ADeploymentWithNoSecretSourceDoesNotStartAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        foreach (IReadOnlyList<SocialProvider> providers in (IReadOnlyList<SocialProvider>[])[[], [Google]])
        {
            var served = new ServerStandIn();
            using IHost deployment = new HostBuilder()
                .ConfigureServices(services => Declared(
                    services.AddSingleton<IHostedService>(served),
                    providers: providers,
                    secretSource: false))
                .Build();

            StartupException refused = await Assert.ThrowsAsync<StartupException>(
                async () => await deployment.StartAsync(cancellationToken));

            Assert.Equal(ErrorCodes.StartupDeclarationMissing, refused.Failure?.Code);
            Assert.Equal("secretSource", refused.Failure?.Details["key"].GetString());
            Assert.False(served.Started);
        }
    }

    /// <summary>
    /// OPS-SEC-001 AC2, AUTH-KEY-002 AC2, D-166: the key-encryption key, the fingerprint
    /// key and the maintenance credential are read through the secret source as the
    /// deployment starts, and one the source cannot answer stops it, naming the secret,
    /// before the web server starts.
    /// </summary>
    /// <param name="key">The secret the source cannot answer.</param>
    /// <returns>The work of running it.</returns>
    [Theory]
    [InlineData("keyEncryptionKeys")]
    [InlineData("fingerprintKeys")]
    [InlineData("maintenanceCredential")]
    public async Task OPS_SEC_001_ASecretTheSourceCannotAnswerStopsTheStartNamingItAsync(string key)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        SecretSourceInMemory answering = HostFixture.Secrets(host.MaintenanceConnectionString);
        var served = new ServerStandIn();
        var secrets = new SecretSourceInMemory(new Dictionary<string, ProviderCredential>(StringComparer.Ordinal))
        {
            KeyEncryptionKeys = key is "keyEncryptionKeys" ? null : answering.KeyEncryptionKeys,
            FingerprintKeys = key is "fingerprintKeys" ? null : answering.FingerprintKeys,
            MaintenanceCredential = key is "maintenanceCredential" ? null : answering.MaintenanceCredential,
        };

        using IHost deployment = new HostBuilder()
            .ConfigureServices(services => Declared(services.AddSingleton<IHostedService>(served), secrets: secrets))
            .Build();

        StartupException refused = await Assert.ThrowsAsync<StartupException>(
            async () => await deployment.StartAsync(cancellationToken));

        Assert.Equal(ErrorCodes.StartupSecretUnavailable, refused.Failure?.Code);
        Assert.Equal(key, refused.Failure?.Details["key"].GetString());
        Assert.False(served.Started);
    }

    /// <summary>
    /// CONV-DESIGN-007, OPS-SEC-001, D-166: every secret is read into the key ring as the
    /// start begins, ahead of every hosted service, so the web server finds the
    /// key-encryption key the source answered already lent when it starts.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task CONV_DESIGN_007_TheSecretsAreReadBeforeTheServerStartsAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        using IHost deployment = new HostBuilder()
            .ConfigureServices(services => Declared(
                services
                    .AddSingleton(provider => new KeyReadingServer(provider.GetRequiredService<IKeyRing>()))
                    .AddSingleton<IHostedService>(provider => provider.GetRequiredService<KeyReadingServer>()),
                secrets: HostFixture.Secrets(host.MaintenanceConnectionString)))
            .Build();

        await deployment.StartAsync(cancellationToken);

        Assert.Equal(1, deployment.Services.GetRequiredService<KeyReadingServer>().CurrentVersion);

        await deployment.StopAsync(cancellationToken);
    }

    /// <summary>
    /// IDN-LIFE-012a, IDN-LIFE-012, D-166: a declared provider's credential is read
    /// through the secret source as the deployment starts, and one the source cannot
    /// answer, answers empty, or answers as a signing credential with a blank issuer,
    /// a blank key identifier or a key that is not a P-256 private key stops it, naming
    /// the provider's credential; a static secret and a signing credential that hold
    /// start it.
    /// </summary>
    /// <param name="fault">What is wrong with the credential, or nothing.</param>
    /// <returns>The work of running it.</returns>
    [Theory]
    [InlineData("unanswered")]
    [InlineData("empty")]
    [InlineData("issuer")]
    [InlineData("keyId")]
    [InlineData("rsa")]
    [InlineData("p384")]
    public async Task IDN_LIFE_012a_ASocialProviderWithoutAUsableCredentialIsRefusedAsync(string fault)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using var p256 = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var p384 = ECDsa.Create(ECCurve.NamedCurves.nistP384);
        using var rsa = RSA.Create(2048);
        ProviderCredential? credential = fault switch
        {
            "unanswered" => null,
            "empty" => ProviderCredential.Secret(ReadOnlyMemory<byte>.Empty),
            "issuer" => ProviderCredential.Signed(" ", "KEY1", p256.ExportPkcs8PrivateKey()),
            "keyId" => ProviderCredential.Signed("TEAM1", "", p256.ExportPkcs8PrivateKey()),
            "rsa" => ProviderCredential.Signed("TEAM1", "KEY1", rsa.ExportPkcs8PrivateKey()),
            _ => ProviderCredential.Signed("TEAM1", "KEY1", p384.ExportPkcs8PrivateKey()),
        };
        var secrets = new SecretSourceInMemory(credential is null
            ? new Dictionary<string, ProviderCredential>(StringComparer.Ordinal)
            : new Dictionary<string, ProviderCredential>(StringComparer.Ordinal) { ["google"] = credential });

        using (IHost refusedHost = Deployed(providers: [Google], secrets: secrets))
        {
            StartupException refused = await Assert.ThrowsAsync<StartupException>(
                async () => await refusedHost.StartAsync(cancellationToken));

            Assert.Equal(ErrorCodes.StartupSecretUnavailable, refused.Failure?.Code);
            Assert.Equal("socialProvider.google", refused.Failure?.Details["key"].GetString());
        }

        var signed = new SecretSourceInMemory(new Dictionary<string, ProviderCredential>(StringComparer.Ordinal)
        {
            ["google"] = ProviderCredential.Signed("TEAM1", "KEY1", p256.ExportPkcs8PrivateKey()),
        });

        SecretSourceInMemory[] holding = [new SecretSourceInMemory(GoogleSecret), signed];

        foreach (SecretSourceInMemory source in holding)
        {
            using IHost started = Deployed(providers: [Google], secrets: source);

            await started.StartAsync(cancellationToken);
            await started.StopAsync(cancellationToken);

            Assert.Equal(["google"], source.Asked);
        }
    }

    /// <summary>
    /// CONV-CODE-007 AC3, D-171: the key ring lends what the deployment read at its start
    /// while the application runs, and once it has stopped, after the worker and the
    /// server, a read is a fault.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task CONV_CODE_007_AC3_AReadAfterTheApplicationStopsThrowsAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        using IHost deployment = Deployed(providers: [Google], secrets: new SecretSourceInMemory(GoogleSecret));
        IKeyRing ring = deployment.Services.GetRequiredService<IKeyRing>();

        await deployment.StartAsync(cancellationToken);

        int lent = ring.BorrowProviderCredential("google", credential => credential.Material.Length)
            .Match(length => length, _ => 0);

        await deployment.StopAsync(cancellationToken);

        Assert.Equal("the-client-secret".Length, lent);
        Assert.Throws<InvalidOperationException>(
            () => ring.BorrowProviderCredential("google", credential => credential.Material.Length));
    }

    /// <summary>
    /// CONV-DESIGN-007 AC5, D-176: the mail server in use is chosen once, as the
    /// deployment starts: the host's own where it registered one, and none where it
    /// registered none and the adapter's endpoint is empty. Asked before the start chose,
    /// it is a fault.
    /// </summary>
    /// <param name="registered">Whether the host registers a mail server of its own.</param>
    /// <returns>The work of running it.</returns>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CONV_DESIGN_007_AC5_TheMailServerInUseIsChosenAtTheStartAsync(bool registered)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        using IHost deployment = Deployed(mail: registered, mailClient: registered);
        IMailServerInUse inUse = deployment.Services.GetRequiredService<IMailServerInUse>();

        Assert.Throws<InvalidOperationException>(() => inUse.Chosen());

        await deployment.StartAsync(cancellationToken);

        Result<IMailServer> chosen = inUse.Chosen();

        await deployment.StopAsync(cancellationToken);

        if (registered)
        {
            Assert.Same(
                deployment.Services.GetRequiredService<IMailServer>(),
                chosen.Match<IMailServer?>(server => server, _ => null));
        }
        else
        {
            Assert.Equal(
                ErrorCodes.MailboxNotFound,
                chosen.Match<ErrorCode?>(_ => null, error => error.Code));
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
    /// the database, and among them the key ring's choice of the mail server in use,
    /// stand at the head of the collection and no request is served before them; the key
    /// ring's reading of the secrets comes before them all, as the start begins (D-160,
    /// CONV-DESIGN-007, D-176). The hosted services are read as the host starts them,
    /// resolved in the order they were registered, so one a factory makes is read by the
    /// type it answers (D-180).
    /// </summary>
    [Fact]
    public void AUTHZ_MODEL_004_AC2_TheChecksStartBeforeEverythingElseRegistered()
    {
        var services = new ServiceCollection();

        services.AddSingleton<IHostedService>(new ServerStandIn());

        Declared(services);
        services.AddLogging();

        using ServiceProvider provider = services.BuildServiceProvider();

        ServiceDescriptor[] hosted = [.. services.Where(service => service.ServiceType == typeof(IHostedService))];
        Type[] leading =
        [
            typeof(SchemaValidationService),
            typeof(SettingsValidationService),
            typeof(ModelValidationService),
            typeof(SendingValidationService),
            typeof(KeyRingService),
            typeof(HandlerValidationService),
            typeof(ConfigurationValidationService),
            typeof(DeclarationValidationService),
            typeof(RedirectValidationService),
            typeof(ProviderStartService),
            typeof(RelayValidationService),
            typeof(LawfulBasisStartService),
        ];

        Assert.Equal(leading, provider.GetServices<IHostedService>().Take(leading.Length).Select(service => service.GetType()));
        Assert.Equal(Enumerable.Range(0, leading.Length), hosted.Take(leading.Length).Select(services.IndexOf));
        Assert.True(typeof(IHostedLifecycleService).IsAssignableFrom(typeof(KeyRingService)));
    }

    /// <summary>
    /// OPS-MIG-002 AC1: a deployment pointed at a database the pipeline did not
    /// migrate is stopped as it starts, with the named error and the migrations it is
    /// behind by; the fault the start throws is what ends the process with a non-zero
    /// exit.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task OPS_MIG_002_AC1_ADeploymentOnAnUnmigratedDatabaseIsRefusedAsync()
    {
        StartupException refused = await UnmigratedAsync(Unmigrated, new ServerStandIn());

        Assert.Equal(ErrorCodes.StartupSchemaMismatch, refused.Failure?.Code);
        Assert.NotEqual(0, refused.Failure?.Details["pending"].GetArrayLength());
    }

    /// <summary>
    /// OPS-MIG-002 AC2: the web server registered after the library is never started
    /// over a database the pipeline did not migrate, so nothing is served in the
    /// mismatched state.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task OPS_MIG_002_AC2_NothingIsServedOverAnUnmigratedDatabaseAsync()
    {
        var served = new ServerStandIn();

        await UnmigratedAsync(Unserved, served);

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

        int before = (await AnnouncedAsync()).Count;

        using (IHost undeclared = Deployed())
        {
            await undeclared.StartAsync(cancellationToken);
            await undeclared.StopAsync(cancellationToken);
        }

        IReadOnlyList<(string?, string?, string?)> warned = [.. (await AnnouncedAsync()).Skip(before)];

        Assert.Equal([("relay-domain-unregistered", "normal", "mail.example.test")], warned);

        await WriteAsync(Declaring, cancellationToken);

        try
        {
            using IHost declared = Deployed();

            await declared.StartAsync(cancellationToken);
            await declared.StopAsync(cancellationToken);

            Assert.Equal(before + 1, (await AnnouncedAsync()).Count);
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
    /// <summary>
    /// OPS-CFG-003 AC3 and D-166: a value outside its bounds is refused at startup and
    /// not at first use, so an outbox interval stored above its ceiling, as a ceiling a
    /// later version tightened leaves it, stops the deployment before the server
    /// starts, naming the key and the ceiling's code and never the stored value.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task OPS_CFG_003_AC3_AStoredValueAboveItsCeilingStopsStartupAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        string key = Settings.OutboxPollInterval.Key.ToString();

        await WriteAsync(
            "INSERT INTO identity.settings (key, value) VALUES ('" + key + "', 'PT2M');",
            cancellationToken);

        try
        {
            var served = new ServerStandIn();
            using IHost deployment = new HostBuilder()
                .ConfigureServices(services => Declared(services.AddSingleton<IHostedService>(served)))
                .Build();

            InvalidOperationException refused = await Assert.ThrowsAsync<InvalidOperationException>(
                async () => await deployment.StartAsync(cancellationToken));

            Assert.False(served.Started);
            Assert.Contains(key, refused.Message, StringComparison.Ordinal);
            Assert.Contains(ErrorCodes.ConfigurationValueAboveCeiling.ToString(), refused.Message, StringComparison.Ordinal);
            Assert.DoesNotContain("PT2M", refused.Message, StringComparison.Ordinal);
        }
        finally
        {
            await WriteAsync("DELETE FROM identity.settings WHERE key = '" + key + "';", cancellationToken);
        }
    }

    /// <summary>
    /// AUTHZ-DERIVE-005 AC5, LIB-HOST-001 AC2: a model declaring a derivation, materialised
    /// or not, whose relationship has no declared source does not start, and the refusal
    /// names the relationship.
    /// </summary>
    /// <param name="materialised">Whether the derivation is materialised.</param>
    /// <returns>The work of running it.</returns>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AUTHZ_DERIVE_005_AC5_ADerivationWithoutItsRelationshipSourceIsRefusedAsync(bool materialised)
    {
        using IHost deployment = Deployed(source: "none", materialised: materialised);

        StartupException refused = await Assert.ThrowsAsync<StartupException>(
            async () => await deployment.StartAsync(TestContext.Current.CancellationToken));

        Assert.Equal(ErrorCodes.StartupDeclarationMissing, refused.Failure?.Code);
        Assert.Equal("reviewer", refused.Failure?.Details["key"].GetString());
    }

    /// <summary>
    /// AUTHZ-DERIVE-005 AC5, LIB-HOST-001 AC2: a relationship source given twice, naming
    /// no declared relationship, answering rows of another type than the derivation's,
    /// or naming a context the container does not give in a scope or whose model does
    /// not map every contract table stops the deployment as it starts, naming the source
    /// and the member at fault; one declared whole starts.
    /// </summary>
    /// <param name="fault">What is wrong with the declaration.</param>
    /// <param name="declaration">The declaration the refusal names.</param>
    /// <param name="field">The member the refusal names.</param>
    /// <returns>The work of running it.</returns>
    [Theory]
    [InlineData("twice", "relationshipSource.reviewer", "relationship")]
    [InlineData("undeclared", "relationshipSource.auditor", "relationship")]
    [InlineData("rows", "relationshipSource.reviewer", "rows")]
    [InlineData("ungiven", "relationshipSource.reviewer", "context")]
    [InlineData("unmapped", "relationshipSource.reviewer", "context")]
    [InlineData("partly mapped", "relationshipSource.reviewer", "context")]
    public async Task AUTHZ_DERIVE_005_AC5_AMalformedRelationshipSourceIsRefusedNamingItAsync(
        string fault,
        string declaration,
        string field)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        using (IHost refusedHost = Deployed(source: fault))
        {
            StartupException refused = await Assert.ThrowsAsync<StartupException>(
                async () => await refusedHost.StartAsync(cancellationToken));

            Assert.Equal(ErrorCodes.StartupDeclarationInvalid, refused.Failure?.Code);
            Assert.Equal(declaration, refused.Failure?.Details["declaration"].GetString());
            Assert.Equal(field, refused.Failure?.Details["field"].GetString());
        }

        using IHost started = Deployed();

        await started.StartAsync(cancellationToken);
        await started.StopAsync(cancellationToken);
    }

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
        IReadOnlyList<SocialProvider>? providers = null,
        ISecretSource? secrets = null,
        bool mailTransport = true,
        bool smsTransport = true,
        bool secretSource = true,
        bool resolver = false,
        string? document = null,
        LandingOrigins? landing = null,
        bool landed = true,
        string source = "whole",
        bool materialised = false) =>
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
                providers: providers,
                secrets: secrets,
                mailTransport: mailTransport,
                smsTransport: smsTransport,
                secretSource: secretSource,
                resolver: resolver,
                document: document,
                landing: landing,
                landed: landed,
                source: source,
                materialised: materialised))
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
        IReadOnlyList<SocialProvider>? providers = null,
        ISecretSource? secrets = null,
        bool mailTransport = true,
        bool smsTransport = true,
        bool secretSource = true,
        bool resolver = false,
        string? document = null,
        LandingOrigins? landing = null,
        bool landed = true,
        string source = "whole",
        bool materialised = false)
    {
        Sourced(services, connection ?? host.ConnectionString, source);

        if (mailTransport)
        {
            services.AddSingleton<IMailTransport>(new MailTransportInMemory());
        }

        if (smsTransport)
        {
            services.AddSingleton<ISmsTransport>(new SmsTransportInMemory());
        }

        if (codec)
        {
            services.AddSingleton(new ImageCodecInMemory().Declared);
        }

        if (resolver)
        {
            services.AddSingleton<IDnsResolver>(new Janus.Authentication.Tests.Organizations.DnsResolverInMemory());
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

        if (landed)
        {
            services.AddSingleton(landing ?? Landing);
        }

        foreach (SocialProvider provider in providers ?? [])
        {
            services.AddSingleton(provider);
        }

        if (secretSource)
        {
            services.AddSingleton<ISecretSource>(secrets ?? HostFixture.Secrets(host.MaintenanceConnectionString));
        }

        return services.AddJanus(
            connection ?? host.ConnectionString,
            HostFixture.Declaration(materialised, document),
            ApplicationKind.Public);
    }

    // LIB-HOST-001: the source of the one relationship the declaration's derivation is
    // over, declared whole or in one of the ways a declaration can fail to hold.
    private static void Sourced(IServiceCollection services, string connection, string source)
    {
        switch (source)
        {
            case "none":
                break;
            case "twice":
                HostFixture.Sourced(services, connection)
                    .AddSingleton(RelationshipSource.Of<HostContext, HostReviewer>("reviewer", context => context.Reviewers));
                break;
            case "undeclared":
                HostFixture.Sourced(services, connection)
                    .AddSingleton(RelationshipSource.Of<HostContext, HostReviewer>("auditor", context => context.Reviewers));
                break;
            case "rows":
                services.AddDbContext<HostContext>(options => options.UseNpgsql(connection))
                    .AddSingleton(RelationshipSource.Of<HostContext, HostDocument>("reviewer", context => context.Documents));
                break;
            case "ungiven":
                services.AddSingleton(
                    RelationshipSource.Of<HostContext, HostReviewer>("reviewer", context => context.Reviewers));
                break;
            case "unmapped":
                services
                    .AddScoped(_ => new UnmappedHostContext(
                        new DbContextOptionsBuilder<UnmappedHostContext>().UseNpgsql(connection).Options))
                    .AddSingleton(RelationshipSource.Of<UnmappedHostContext, HostReviewer>("reviewer", context => context.Reviewers));
                break;
            case "partly mapped":
                services
                    .AddScoped(_ => new PartlyMappedHostContext(
                        new DbContextOptionsBuilder<PartlyMappedHostContext>().UseNpgsql(connection).Options))
                    .AddSingleton(RelationshipSource.Of<PartlyMappedHostContext, HostReviewer>("reviewer", context => context.Reviewers));
                break;
            default:
                HostFixture.Sourced(services, connection);
                break;
        }
    }

    // IDN-ATTR-002: an organization shows photos by its key, which is a settings row
    // and nothing the declaration can carry.
    private async Task ShowingPhotosAsync(CancellationToken cancellationToken) =>
        await WriteAsync(
            "INSERT INTO identity.settings (key, value) VALUES ('" + Showing + "', '{\"photos\":true}');",
            cancellationToken);

    // OPS-MIG-002: a deployment over a database created empty under the name given,
    // with the web server's stand-in registered as the host registers it, and what its
    // start throws.
    private async Task<StartupException> UnmigratedAsync(string database, ServerStandIn served)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await WriteAsync(
            "CREATE DATABASE " + database + " TEMPLATE template0 "
                + "LOCALE_PROVIDER icu ICU_LOCALE 'und' LC_COLLATE 'C' LC_CTYPE 'C'",
            cancellationToken);

        using IHost deployment = new HostBuilder()
            .ConfigureServices(services => Declared(
                services.AddSingleton<IHostedService>(served),
                connection: new NpgsqlConnectionStringBuilder(host.ConnectionString)
                {
                    Database = database,
                }.ConnectionString))
            .Build();

        return await Assert.ThrowsAsync<StartupException>(
            async () => await deployment.StartAsync(cancellationToken));
    }

    // What the library announced, as the rows of its events table: an alert is
    // written with its event (OPS-ALERT-001, CONV-DESIGN-002).
    private async Task<IReadOnlyList<(string?, string?, string?)>> AnnouncedAsync()
    {
        await using NpgsqlConnection connection = await host.OpenAsync();

        return [.. await connection.QueryAsync<(string?, string?, string?)>(
            """
            SELECT payload->>'Condition', payload->>'Severity', payload->'Details'->>'domain'
            FROM identity.events WHERE kind = 'AlertRaised' ORDER BY id
            """)];
    }

    private async Task WriteAsync(string statement, CancellationToken cancellationToken)
    {
        await using NpgsqlConnection connection = await host.OpenAsync();

        await connection.ExecuteAsync(new CommandDefinition(
            statement,
            cancellationToken: cancellationToken));
    }

    // A subscriber of the host's own under the name given, covering nothing.
    private sealed class Named(string name) : ISubjectEventSubscriber
    {
        public string Name => name;

        public bool Required => false;

        public IReadOnlyCollection<ResourceType> Covers { get; } = [];

        public ValueTask<Result> HandleAsync(SubjectEvent raised, CancellationToken cancellationToken) =>
            ValueTask.FromResult(Result.Success());
    }

    // A subscriber of the host's that took the name the erasure ledger's confirmation
    // is recorded under.
    private sealed class Impostor : ISubjectEventSubscriber
    {
        public string Name => "erasure-ledger";

        public bool Required => false;

        public IReadOnlyCollection<ResourceType> Covers { get; } = [];

        public ValueTask<Result> HandleAsync(SubjectEvent raised, CancellationToken cancellationToken) =>
            ValueTask.FromResult(Result.Success());
    }

    // A hosted service of the host's own, registered before the library is, standing
    // for the web server the deployment starts.
    // A web server that, as it starts, reads the key-encryption key the ring lends.
    private sealed class KeyReadingServer(IKeyRing ring) : IHostedService
    {
        public int? CurrentVersion { get; private set; }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            CurrentVersion = ring.BorrowKeyEncryptionKeys(keys => keys.CurrentVersion).Match(version => (int?)version, _ => null);

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

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
