using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Core.Configuration;
using Janus.Hosting.Mailboxes;
using Janus.Hosting.Tests.Passwords;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Janus.Hosting.Tests.Mailboxes;

/// <summary>
/// The shipped mail-server adapter against a real server of the release it is written
/// for, chosen by the start as a deployment's is and reaching the server through its own
/// client over TLS (INT-MAIL-001, CONV-TEST-002, D-176, D-177, D-224).
/// </summary>
/// <param name="server">The server, one for the class.</param>
[Trait("kind", "integration")]
public sealed class JmapMailServerAgainstStalwartTests(StalwartServer server) : IClassFixture<StalwartServer>
{
    private static readonly IReadOnlyDictionary<string, ProviderCredential> NoCredentials =
        new Dictionary<string, ProviderCredential>(StringComparer.Ordinal);

    // The criteria the server decides with the management key: INT-MAIL-001 criteria 3
    // to 6, and INT-MAIL-010's calls (INT-MAIL-001 AC7).
    private static readonly string[] Decided =
    [
        "INT_MAIL_001_AC3_",
        "INT_MAIL_001_AC4_",
        "INT_MAIL_001_AC5_",
        "INT_MAIL_001_AC6_",
        "INT_MAIL_010_AC1_",
    ];

    private readonly ConcurrentQueue<Sent> _sent = new();

    /// <summary>
    /// INT-MAIL-001 AC3: every provisioning operation, a create, a change of state each
    /// way, the listing and a removal, is a JMAP request posted to the endpoint's
    /// <c>/jmap</c> with the core and the server's capabilities, and the server applies
    /// each.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task INT_MAIL_001_AC3_EveryProvisioningOperationIsAJmapRequestTheServerAppliesAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        var mailbox = Mailbox.Drawn();

        await using Started started = await StartedAsync();

        Assert.True(Succeeded(await started.Server.ProvisionAsync(mailbox.Push(MailboxState.Disabled), cancellationToken)));
        Assert.True(Succeeded(await started.Server.ProvisionAsync(mailbox.Push(MailboxState.Enabled), cancellationToken)));
        Assert.True(Succeeded(await started.Server.ProvisionAsync(mailbox.Push(MailboxState.Disabled), cancellationToken)));
        Assert.Contains(
            new HostedMailbox(mailbox.Id, mailbox.Address, Enabled: false),
            Value(await started.Server.MailboxesAsync(cancellationToken)));
        Assert.True(Succeeded(await started.Server.ProvisionAsync(mailbox.Push(MailboxState.Removed), cancellationToken)));
        Assert.Null(await server.AccountAsync(mailbox.Name));

        Assert.NotEmpty(_sent);
        Assert.All(_sent, sent =>
        {
            Assert.Equal(HttpMethod.Post, sent.Method);
            Assert.Equal(new Uri(server.Endpoint, "jmap"), sent.Address);
            Assert.Equal(
                ["urn:ietf:params:jmap:core", "urn:stalwart:jmap"],
                ((JsonArray)sent.Body["using"]!).Select(capability => (string)capability!));
            Assert.All(
                (JsonArray)sent.Body["methodCalls"]!,
                call => Assert.StartsWith("x:", (string)call![0]!, StringComparison.Ordinal));
        });
    }

    /// <summary>
    /// INT-MAIL-001 AC4, D-177: a push of any state, a removal included, that meets at the
    /// mailbox's name an account whose description does not carry the mailbox's
    /// identifier adopts nothing, changes nothing at the server and is answered
    /// <c>integration.mailserver.conflict</c>.
    /// </summary>
    /// <param name="state">The state pushed.</param>
    /// <param name="description">What the account at the server carries.</param>
    /// <returns>The work of the test.</returns>
    [Theory]
    [InlineData(MailboxState.Disabled, null)]
    [InlineData(MailboxState.Enabled, "made by hand")]
    [InlineData(MailboxState.Removed, "0192f1a0-5c3e-7a10-8f00-00000000b002")]
    public async Task INT_MAIL_001_AC4_AnAccountNotCarryingTheIdentifierIsNeverAdoptedAsync(
        MailboxState state,
        string? description)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        var mailbox = Mailbox.Drawn();

        _ = await server.CreateAsync(mailbox.Name, description, new JsonObject { ["@type"] = "Inherit" });

        string before = (await server.AccountAsync(mailbox.Name))!.Value.GetRawText();

        await using Started started = await StartedAsync();

        Result pushed = await started.Server.ProvisionAsync(mailbox.Push(state), cancellationToken);

        Assert.Equal(ErrorCodes.MailServerConflict, Refusal(pushed));
        Assert.Equal(before, (await server.AccountAsync(mailbox.Name))!.Value.GetRawText());
        Assert.DoesNotContain(
            _sent.SelectMany(sent => (JsonArray)sent.Body["methodCalls"]!),
            call => (string)call![0]! == "x:Account/set");
    }

    /// <summary>
    /// INT-MAIL-001 AC4, INT-MAIL-007 AC1: a push the library replays converges on the
    /// account it created and creates nothing twice, and a change of state after it acts
    /// on that same account.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task INT_MAIL_001_AC4_AReplayedPushConvergesOnTheAccountItCreatedAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        var mailbox = Mailbox.Drawn();
        MailboxPush push = mailbox.Push(MailboxState.Disabled);

        await using Started started = await StartedAsync();

        Assert.True(Succeeded(await started.Server.ProvisionAsync(push, cancellationToken)));

        string created = (await server.AccountAsync(mailbox.Name))!.Value.GetProperty("id").GetString()!;

        Assert.True(Succeeded(await started.Server.ProvisionAsync(push, cancellationToken)));
        Assert.True(Succeeded(await started.Server.ProvisionAsync(mailbox.Push(MailboxState.Enabled), cancellationToken)));

        JsonElement account = (await server.AccountAsync(mailbox.Name))!.Value;

        Assert.Equal(created, account.GetProperty("id").GetString());
        Assert.Empty(Members(account, "disabledPermissions"));
        Assert.Single(
            Value(await started.Server.MailboxesAsync(cancellationToken)),
            hosted => hosted.Mailbox == mailbox.Id);
    }

    /// <summary>
    /// INT-MAIL-001 AC5, D-176: an <c>enabled</c> push to an account whose permissions
    /// replace the inherited ones and do not enable <c>authenticate</c> enables it and
    /// takes it out of the disabled ones, changes no other permission and keeps the kind,
    /// and the listing then answers the account enabled.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task INT_MAIL_001_AC5_AnEnabledPushToAReplaceAccountEnablesAuthenticationAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        var mailbox = Mailbox.Drawn();

        _ = await server.CreateAsync(mailbox.Name, mailbox.Id.ToString(), new JsonObject
        {
            ["@type"] = "Replace",
            ["enabledPermissions"] = new JsonObject { ["emailSend"] = true },
            ["disabledPermissions"] = new JsonObject { ["authenticate"] = true, ["emailReceive"] = true },
        });

        await using Started started = await StartedAsync();

        Assert.True(Succeeded(await started.Server.ProvisionAsync(mailbox.Push(MailboxState.Enabled), cancellationToken)));

        JsonElement account = (await server.AccountAsync(mailbox.Name))!.Value;

        Assert.Equal("Replace", account.GetProperty("permissions").GetProperty("@type").GetString());
        Assert.Equal(["authenticate", "emailSend"], Members(account, "enabledPermissions"));
        Assert.Equal(["emailReceive"], Members(account, "disabledPermissions"));
        Assert.Contains(
            new HostedMailbox(mailbox.Id, mailbox.Address, Enabled: true),
            Value(await started.Server.MailboxesAsync(cancellationToken)));
    }

    /// <summary>
    /// INT-MAIL-001 AC6, D-176: a <c>disabled</c> push to an account that inherits its
    /// permissions leaves it merging them with <c>authenticate</c> its only disabled
    /// permission; one to an account that merges or replaces them adds
    /// <c>authenticate</c> to its disabled permissions and changes nothing else; the
    /// listing then answers the account disabled.
    /// </summary>
    /// <param name="kind">How the account holds its permissions.</param>
    /// <returns>The work of the test.</returns>
    [Theory]
    [InlineData("Inherit")]
    [InlineData("Merge")]
    [InlineData("Replace")]
    public async Task INT_MAIL_001_AC6_ADisabledPushChangesAuthenticationAloneAsync(string kind)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        var mailbox = Mailbox.Drawn();

        _ = await server.CreateAsync(mailbox.Name, mailbox.Id.ToString(), kind is "Inherit"
            ? new JsonObject { ["@type"] = kind }
            : new JsonObject
            {
                ["@type"] = kind,
                ["enabledPermissions"] = new JsonObject { ["authenticate"] = true, ["emailSend"] = true },
                ["disabledPermissions"] = new JsonObject { ["emailReceive"] = true },
            });

        await using Started started = await StartedAsync();

        Assert.True(Succeeded(await started.Server.ProvisionAsync(mailbox.Push(MailboxState.Disabled), cancellationToken)));

        JsonElement account = (await server.AccountAsync(mailbox.Name))!.Value;

        if (kind is "Inherit")
        {
            Assert.Equal("Merge", account.GetProperty("permissions").GetProperty("@type").GetString());
            Assert.Equal(["authenticate"], Members(account, "disabledPermissions"));
            Assert.Empty(Members(account, "enabledPermissions"));
        }
        else
        {
            Assert.Equal(kind, account.GetProperty("permissions").GetProperty("@type").GetString());
            Assert.Equal(["authenticate", "emailSend"], Members(account, "enabledPermissions"));
            Assert.Equal(["authenticate", "emailReceive"], Members(account, "disabledPermissions"));
        }

        Assert.Contains(
            new HostedMailbox(mailbox.Id, mailbox.Address, Enabled: false),
            Value(await started.Server.MailboxesAsync(cancellationToken)));
    }

    /// <summary>
    /// INT-MAIL-001, D-177: a removal destroys the account carrying the mailbox's
    /// identifier, and a removal that finds no account under the mailbox's name is done.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task INT_MAIL_001_ARemovalDestroysTheMailboxsOwnAccountAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        var mailbox = Mailbox.Drawn();

        await using Started started = await StartedAsync();

        Assert.True(Succeeded(await started.Server.ProvisionAsync(mailbox.Push(MailboxState.Enabled), cancellationToken)));
        Assert.NotNull(await server.AccountAsync(mailbox.Name));
        Assert.True(Succeeded(await started.Server.ProvisionAsync(mailbox.Push(MailboxState.Removed), cancellationToken)));
        Assert.Null(await server.AccountAsync(mailbox.Name));
        Assert.True(Succeeded(await started.Server.ProvisionAsync(mailbox.Push(MailboxState.Removed), cancellationToken)));
        Assert.DoesNotContain(
            Value(await started.Server.MailboxesAsync(cancellationToken)),
            hosted => hosted.Mailbox == mailbox.Id);
    }

    /// <summary>
    /// INT-MAIL-001, INT-MAIL-007: the listing answers each account the server holds
    /// with the identifier its description carries, none where it carries none, its
    /// address and whether it is enabled.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task INT_MAIL_001_TheListingAnswersEachAccountAsTheServerHoldsItAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        var enabled = Mailbox.Drawn();
        var disabled = Mailbox.Drawn();
        var byHand = Mailbox.Drawn();

        _ = await server.CreateAsync(byHand.Name, description: null, new JsonObject { ["@type"] = "Inherit" });

        await using Started started = await StartedAsync();

        Assert.True(Succeeded(await started.Server.ProvisionAsync(enabled.Push(MailboxState.Enabled), cancellationToken)));
        Assert.True(Succeeded(await started.Server.ProvisionAsync(disabled.Push(MailboxState.Disabled), cancellationToken)));

        IReadOnlyList<HostedMailbox> listed = Value(await started.Server.MailboxesAsync(cancellationToken));

        Assert.Contains(new HostedMailbox(enabled.Id, enabled.Address, Enabled: true), listed);
        Assert.Contains(new HostedMailbox(disabled.Id, disabled.Address, Enabled: false), listed);
        Assert.Contains(new HostedMailbox(null, byHand.Address, Enabled: true), listed);
    }

    /// <summary>
    /// INT-MAIL-010 AC1 to AC3, INT-MAIL-001 AC7 (D-226): an app password is created,
    /// listed and revoked by one call each with the management key on the account that
    /// carries the mailbox's identifier, named by the call's <c>accountId</c>; the server
    /// generates the secret, which signs a mail client in until it is revoked and not
    /// after.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task INT_MAIL_010_AC1_AnAppPasswordIsManagedOnTheAccountOfTheMailboxHeldAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        var mailbox = Mailbox.Drawn();
        DateTimeOffset expiresAt = DateTimeOffset.UtcNow.AddDays(30);

        await using Started started = await StartedAsync();

        Assert.True(Succeeded(await started.Server.ProvisionAsync(mailbox.Push(MailboxState.Enabled), cancellationToken)));

        string account = (await server.AccountAsync(mailbox.Name))!.Value.GetProperty("id").GetString()!;

        _sent.Clear();

        IssuedAppPassword issued = Value(await started.Server.CreateAppPasswordAsync(
            mailbox.Id,
            mailbox.Address,
            "Phone",
            expiresAt,
            cancellationToken));

        Assert.NotEmpty(issued.Secret);
        Assert.Single(
            _sent.SelectMany(sent => (JsonArray)sent.Body["methodCalls"]!),
            call => (string)call![0]! == "x:AppPassword/set");
        Assert.All(
            _sent.SelectMany(sent => (JsonArray)sent.Body["methodCalls"]!)
                .Where(call => ((string)call![0]!).StartsWith("x:AppPassword/", StringComparison.Ordinal)),
            call => Assert.Equal(account, (string)call![1]!["accountId"]!));

        AppPassword listed = Assert.Single(Value(await started.Server.AppPasswordsAsync(
            mailbox.Id,
            mailbox.Address,
            cancellationToken)));

        Assert.Equal((issued.Id, "Phone"), (listed.Id, listed.Label));
        Assert.Equal(expiresAt.ToUnixTimeSeconds(), listed.ExpiresAt!.Value.ToUnixTimeSeconds());
        Assert.Equal(HttpStatusCode.OK, await server.SignedInAsync(mailbox.Address.Value, issued.Secret));

        Assert.True(Succeeded(await started.Server.RevokeAppPasswordAsync(
            mailbox.Id,
            mailbox.Address,
            issued.Id,
            cancellationToken)));
        Assert.Empty(Value(await started.Server.AppPasswordsAsync(mailbox.Id, mailbox.Address, cancellationToken)));
        Assert.Equal(HttpStatusCode.Unauthorized, await server.SignedInAsync(mailbox.Address.Value, issued.Secret));
        Assert.Equal(
            ErrorCodes.CredentialNotFound,
            Refusal(await started.Server.RevokeAppPasswordAsync(
                mailbox.Id,
                mailbox.Address,
                issued.Id,
                cancellationToken)));
    }

    /// <summary>
    /// INT-MAIL-010, INT-MAIL-001 AC7 (D-226): where the server holds no account under
    /// the mailbox's name, or one that does not carry its identifier, each app-password
    /// call fails as a failed call does and no app password is read, made or destroyed
    /// on any account.
    /// </summary>
    /// <param name="exists">Whether an account the library did not make holds the name.</param>
    /// <returns>The work of the test.</returns>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task INT_MAIL_010_ACallActsOnNoAccountButTheOneCarryingTheMailboxAsync(bool exists)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        var mailbox = Mailbox.Drawn();

        if (exists)
        {
            _ = await server.CreateAsync(mailbox.Name, description: null, new JsonObject { ["@type"] = "Inherit" });
        }

        await using Started started = await StartedAsync();

        Assert.Equal(
            ErrorCodes.SystemFault,
            (await started.Server.AppPasswordsAsync(mailbox.Id, mailbox.Address, cancellationToken))
                .Match<ErrorCode?>(_ => null, error => error.Code));
        Assert.Equal(
            ErrorCodes.SystemFault,
            (await started.Server.CreateAppPasswordAsync(mailbox.Id, mailbox.Address, "Phone", null, cancellationToken))
                .Match<ErrorCode?>(_ => null, error => error.Code));
        Assert.Equal(
            ErrorCodes.SystemFault,
            Refusal(await started.Server.RevokeAppPasswordAsync(
                mailbox.Id,
                mailbox.Address,
                AppPasswordId.Parse("p0001"),
                cancellationToken)));
        Assert.DoesNotContain(
            _sent.SelectMany(sent => (JsonArray)sent.Body["methodCalls"]!),
            call => ((string)call![0]!).StartsWith("x:AppPassword/", StringComparison.Ordinal));
    }

    /// <summary>
    /// CONV-TEST-002 AC5, INT-MAIL-001 AC7 for criteria 3 to 6 and INT-MAIL-010's calls:
    /// every criterion of the adapter the server decides with the management key has its
    /// test here, against a server whose image this class names by digest and which
    /// started as the release the adapter is written for.
    /// </summary>
    [Fact]
    public void CONV_TEST_002_AC5_TheServerDecidingTheCriteriaIsItsReleaseNamedByDigest()
    {
        string[] tested =
        [
            .. typeof(JmapMailServerAgainstStalwartTests)
                .GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(method => method.GetCustomAttributes<FactAttribute>(inherit: true).Any())
                .Select(method => method.Name),
        ];

        Assert.Matches(new Regex("^stalwartlabs/stalwart@sha256:[0-9a-f]{64}$", RegexOptions.None, TimeSpan.FromSeconds(5)), StalwartServer.Image);
        Assert.StartsWith("1.0.", server.Release, StringComparison.Ordinal);
        Assert.All(Decided, criterion => Assert.Contains(
            tested,
            name => name.StartsWith(criterion, StringComparison.Ordinal)));
    }

    /// <summary>
    /// CONV-TEST-002 (D-224): the adapter reaches the server over TLS, through its own
    /// client, trusting the server's certificate and nothing more: the same client not
    /// given that trust reaches nothing.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task CONV_TEST_002_TheServerIsReachedOverTlsTrustingItsCertificateAloneAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await using Started trusting = await StartedAsync();
        await using Started untrusting = await StartedAsync(trusted: false);

        Assert.Equal("https", server.Endpoint.Scheme);
        Assert.NotEmpty(Value(await trusting.Server.MailboxesAsync(cancellationToken)));
        Assert.Equal(
            ErrorCodes.SystemFault,
            (await untrusting.Server.MailboxesAsync(cancellationToken)).Match<ErrorCode?>(_ => null, error => error.Code));
    }

    private static TValue Value<TValue>(Result<TValue> result) =>
        result.Match(value => value, error => throw new InvalidOperationException(error.Code.ToString()));

    private static bool Succeeded(Result result) => result.Match(() => true, _ => false);

    private static ErrorCode? Refusal(Result result) => result.Match<ErrorCode?>(() => null, error => error.Code);

    private static string[] Members(JsonElement account, string set) =>
        account.GetProperty("permissions").TryGetProperty(set, out JsonElement members)
            ? [.. members.EnumerateObject()
                .Where(member => member.Value.ValueKind is JsonValueKind.True)
                .Select(member => member.Name)
                .Order(StringComparer.Ordinal)]
            : [];

    private static IMailServer Chosen(IServiceProvider services) =>
        services.GetRequiredService<IMailServerInUse>().Chosen().Match(
            chosen => chosen,
            error => throw new InvalidOperationException(error.Code.ToString()));

    // The deployment started as far as the choice of its mail server: the endpoint the
    // configuration holds, the management key the secret source holds, and the adapter's
    // client given the connection that trusts the server's certificate, or the
    // framework's own connection where it is not.
    private async Task<Started> StartedAsync(bool trusted = true)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        var configuration = new ConfigurationInMemory();
        var services = new ServiceCollection();

        configuration.Set(Settings.IntegrationMailServerEndpoint, server.Endpoint.AbsoluteUri);
        services.AddSingleton<IConfigurationStore>(configuration);
        services.AddSingleton<ISecretSource>(new SecretSourceInMemory(NoCredentials)
        {
            MailServerSecret = server.ManagementKey(),
        });
        services.AddSingleton<Janus.Privacy.SubjectKeys.ISubjectKeyStore>(
            new Janus.Privacy.Tests.SubjectKeys.SubjectKeyStoreInMemory());
        services.AddCoreArea();
        services.AddSingleton<JmapMailServer>();
        IHttpClientBuilder client = services.AddHttpClient(JmapMailServer.Channel)
            .AddHttpMessageHandler(() => new Recording(_sent));

        if (trusted)
        {
            _ = client.ConfigurePrimaryHttpMessageHandler(server.Trusting);
        }
        services.AddSingleton(KeyRingRegistration.Service);

        ServiceProvider provider = services.BuildServiceProvider();
        KeyRingService ring = provider.GetRequiredService<KeyRingService>();

        await ring.StartingAsync(cancellationToken);
        await ring.StartAsync(cancellationToken);

        return new Started(provider, Chosen(provider));
    }

    // A mailbox of the library's, under a name no other test of the class uses.
    private sealed record Mailbox(MailboxId Id, string Name, EmailAddress Address)
    {
        public static Mailbox Drawn()
        {
            string name = "m" + Guid.NewGuid().ToString("N")[..16];

            Assert.True(EmailAddress.TryParse(name + "@" + StalwartServer.Domain, out EmailAddress address));

            return new Mailbox(new MailboxId(Guid.CreateVersion7()), name, address);
        }

        public MailboxPush Push(MailboxState state) => new(Id, Guid.CreateVersion7(), Address, state);
    }

    // What the adapter sent: the method, the address and the request's body.
    private sealed record Sent(HttpMethod Method, Uri Address, JsonNode Body);

    // Writes down each request the adapter's client sends, and sends it on unchanged.
    private sealed class Recording(ConcurrentQueue<Sent> sent) : DelegatingHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            string body = request.Content is null
                ? "null"
                : await request.Content.ReadAsStringAsync(cancellationToken);

            sent.Enqueue(new Sent(request.Method, request.RequestUri!, JsonNode.Parse(body)!));

            return await base.SendAsync(request, cancellationToken);
        }
    }

    // A deployment started as far as the choice of its mail server.
    private sealed record Started(ServiceProvider Services, IMailServer Server) : IAsyncDisposable
    {
        public ValueTask DisposeAsync() => Services.DisposeAsync();
    }
}
