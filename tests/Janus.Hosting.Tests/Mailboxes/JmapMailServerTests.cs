using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json.Nodes;
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
/// The shipped mail-server adapter against an in-memory JMAP management interface, and
/// the start that chooses it (INT-MAIL-001, INT-MAIL-006, INT-MAIL-006a, INT-MAIL-007,
/// INT-MAIL-010, CONV-DESIGN-007, D-166, D-176, D-177).
/// </summary>
[Trait("kind", "unit")]
public sealed class JmapMailServerTests : IDisposable
{
    private const string Address = "ada@example.test";

    private const string Endpoint = "https://mail.example.test/";

    private const string PersonToken = "ada-token";

    private static readonly MailboxId Mailbox = new(Guid.Parse("0192f1a0-5c3e-7a10-8f00-00000000a001"));

    private static readonly MailboxId Another = new(Guid.Parse("0192f1a0-5c3e-7a10-8f00-00000000b002"));

    private static readonly IReadOnlyDictionary<string, ProviderCredential> NoCredentials =
        new Dictionary<string, ProviderCredential>(StringComparer.Ordinal);

    private readonly JmapServerInMemory _server = new();

    private readonly ConfigurationInMemory _configuration = new();

    /// <summary>
    /// Holds the domain every mailbox here is in, and the endpoint the start reads.
    /// </summary>
    public JmapMailServerTests()
    {
        _ = _server.Domain("example.test");
        _server.Knows(PersonToken, "ada");
        _configuration.Set(Settings.IntegrationMailServerEndpoint, Endpoint);
    }

    /// <summary>
    /// INT-MAIL-001 AC3: every provisioning and app-password operation is one JMAP
    /// request posted to the endpoint's <c>/jmap</c> with the core and the server's
    /// capabilities, and nothing else is called.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task INT_MAIL_001_AC3_EveryOperationIsOneJmapRequestAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await using Started started = await StartedAsync();

        Assert.True(Succeeded(await started.Server.ProvisionAsync(Push(MailboxState.Disabled), cancellationToken)));
        Assert.True(Succeeded(await started.Server.ProvisionAsync(Push(MailboxState.Enabled), cancellationToken)));
        _ = await started.Server.MailboxesAsync(cancellationToken);
        IssuedAppPassword issued = Value(await started.Server.CreateAppPasswordAsync(PersonToken, "Phone", null, cancellationToken));
        _ = await started.Server.AppPasswordsAsync(PersonToken, cancellationToken);
        Assert.True(Succeeded(await started.Server.RevokeAppPasswordAsync(PersonToken, issued.Id, cancellationToken)));
        Assert.True(Succeeded(await started.Server.ProvisionAsync(Push(MailboxState.Removed), cancellationToken)));

        Assert.NotEmpty(_server.Requests);
        Assert.All(_server.Requests, request =>
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal(new Uri("https://mail.example.test/jmap"), request.Address);
            Assert.Equal(
                ["urn:ietf:params:jmap:core", "urn:stalwart:jmap"],
                ((JsonArray)request.Body!["using"]!).Select(capability => (string)capability!));
            Assert.All(
                (JsonArray)request.Body["methodCalls"]!,
                call => Assert.StartsWith("x:", (string)call![0]!, StringComparison.Ordinal));
        });
    }

    /// <summary>
    /// INT-MAIL-006 AC1c: a reserved mailbox is created as a user named by the local part
    /// in its domain, carrying the mailbox's identifier, with authentication disabled, and
    /// the listing answers it disabled under that identifier.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task INT_MAIL_006_AC1c_AReservedMailboxIsCreatedWithAuthenticationDisabledAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await using Started started = await StartedAsync();

        Assert.True(Succeeded(await started.Server.ProvisionAsync(Push(MailboxState.Disabled), cancellationToken)));

        JsonObject account = Assert.Single(_server.At(Address));

        Assert.Equal("User", (string?)account["@type"]);
        Assert.Equal("ada", (string?)account["name"]);
        Assert.Equal(Mailbox.ToString(), (string?)account["description"]);
        Assert.Equal("Merge", (string?)account["permissions"]!["@type"]);
        Assert.Equal(["authenticate"], Members(account, "disabledPermissions"));
        Assert.Empty(Members(account, "enabledPermissions"));
        Assert.Equal(
            [new HostedMailbox(Mailbox, Address, Enabled: false)],
            Value(await started.Server.MailboxesAsync(cancellationToken)));
    }

    /// <summary>
    /// INT-MAIL-006a AC1: a push of <c>disabled</c> to an enabled mailbox disables its
    /// authentication, and a push of <c>enabled</c> gives it back.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task INT_MAIL_006a_AC1_ADisablePushDisablesAuthenticationAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await using Started started = await StartedAsync();

        Assert.True(Succeeded(await started.Server.ProvisionAsync(Push(MailboxState.Enabled), cancellationToken)));
        Assert.True(Assert.Single(Value(await started.Server.MailboxesAsync(cancellationToken))).Enabled);

        Assert.True(Succeeded(await started.Server.ProvisionAsync(Push(MailboxState.Disabled), cancellationToken)));
        Assert.False(Assert.Single(Value(await started.Server.MailboxesAsync(cancellationToken))).Enabled);

        Assert.True(Succeeded(await started.Server.ProvisionAsync(Push(MailboxState.Enabled), cancellationToken)));
        Assert.True(Assert.Single(Value(await started.Server.MailboxesAsync(cancellationToken))).Enabled);
        Assert.Single(_server.At(Address));
    }

    /// <summary>
    /// INT-MAIL-007 AC1, INT-MAIL-001 AC4: a push made again converges on the account it
    /// created and creates nothing twice, and so does one whose query missed the account
    /// and whose create the server refused because the name exists.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task INT_MAIL_007_AC1_AReplayedPushCreatesNoSecondAccountAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await using Started started = await StartedAsync();
        MailboxPush push = Push(MailboxState.Disabled);

        Assert.True(Succeeded(await started.Server.ProvisionAsync(push, cancellationToken)));
        Assert.True(Succeeded(await started.Server.ProvisionAsync(push, cancellationToken)));

        _server.QueriesMissing = 1;

        Assert.True(Succeeded(await started.Server.ProvisionAsync(Push(MailboxState.Enabled), cancellationToken)));

        JsonObject account = Assert.Single(_server.At(Address));

        Assert.Empty(Members(account, "disabledPermissions"));
    }

    /// <summary>
    /// INT-MAIL-001 AC4, D-177: a push of any state, a removal included, that meets at
    /// the mailbox's name an account not carrying the mailbox's identifier adopts
    /// nothing, changes nothing at the server and is answered
    /// <c>integration.mailserver.conflict</c>; so is a create the server refused for such
    /// an account.
    /// </summary>
    /// <param name="state">The state pushed.</param>
    /// <param name="description">What the account at the server carries.</param>
    /// <param name="racing">Whether the query misses the account, so the create meets it.</param>
    /// <returns>The work of the test.</returns>
    [Theory]
    [InlineData(MailboxState.Disabled, null, false)]
    [InlineData(MailboxState.Enabled, "made by hand", false)]
    [InlineData(MailboxState.Removed, "0192f1a0-5c3e-7a10-8f00-00000000b002", false)]
    [InlineData(MailboxState.Disabled, "0192f1a0-5c3e-7a10-8f00-00000000b002", true)]
    public async Task INT_MAIL_001_AC4_AnAccountNotCarryingTheIdentifierIsNeverAdoptedAsync(
        MailboxState state,
        string? description,
        bool racing)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await using Started started = await StartedAsync();

        _ = _server.Account(Address, description, new JsonObject { ["@type"] = "Inherit" });

        string before = Assert.Single(_server.At(Address)).ToJsonString();

        _server.QueriesMissing = racing ? 1 : 0;

        Result pushed = await started.Server.ProvisionAsync(Push(state), cancellationToken);

        Assert.Equal(ErrorCodes.MailServerConflict, Refusal(pushed));
        Assert.Equal(before, Assert.Single(_server.At(Address)).ToJsonString());
        Assert.DoesNotContain(
            _server.Requests.SelectMany(request => (JsonArray)request.Body!["methodCalls"]!),
            call => (string)call![0]! == "x:Account/set" && call[1]!["create"] is null);
    }

    /// <summary>
    /// INT-MAIL-001, D-177: a removal destroys the account carrying the mailbox's
    /// identifier, and a removal that finds no account under the mailbox's name is done
    /// without a change.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task INT_MAIL_001_ARemovalDestroysTheMailboxsOwnAccountAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await using Started started = await StartedAsync();

        Assert.True(Succeeded(await started.Server.ProvisionAsync(Push(MailboxState.Disabled), cancellationToken)));
        Assert.True(Succeeded(await started.Server.ProvisionAsync(Push(MailboxState.Removed), cancellationToken)));
        Assert.Empty(_server.At(Address));

        int requests = _server.Requests.Count;

        Assert.True(Succeeded(await started.Server.ProvisionAsync(Push(MailboxState.Removed), cancellationToken)));
        Assert.DoesNotContain(
            _server.Requests.Skip(requests).SelectMany(request => (JsonArray)request.Body!["methodCalls"]!),
            call => (string)call![0]! == "x:Account/set");
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

        await using Started started = await StartedAsync();

        _ = _server.Account(Address, Mailbox.ToString(), new JsonObject
        {
            ["@type"] = "Replace",
            ["enabledPermissions"] = new JsonObject { ["emailSend"] = true },
            ["disabledPermissions"] = new JsonObject { ["authenticate"] = true, ["emailReceive"] = true },
        });

        Assert.True(Succeeded(await started.Server.ProvisionAsync(Push(MailboxState.Enabled), cancellationToken)));

        JsonObject account = Assert.Single(_server.At(Address));

        Assert.Equal("Replace", (string?)account["permissions"]!["@type"]);
        Assert.Equal(["authenticate", "emailSend"], Members(account, "enabledPermissions"));
        Assert.Equal(["emailReceive"], Members(account, "disabledPermissions"));
        Assert.True(Assert.Single(Value(await started.Server.MailboxesAsync(cancellationToken))).Enabled);
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

        await using Started started = await StartedAsync();

        _ = _server.Account(Address, Mailbox.ToString(), kind is "Inherit"
            ? new JsonObject { ["@type"] = kind }
            : new JsonObject
            {
                ["@type"] = kind,
                ["enabledPermissions"] = new JsonObject { ["authenticate"] = true, ["emailSend"] = true },
                ["disabledPermissions"] = new JsonObject { ["emailReceive"] = true },
            });

        Assert.True(Succeeded(await started.Server.ProvisionAsync(Push(MailboxState.Disabled), cancellationToken)));

        JsonObject account = Assert.Single(_server.At(Address));

        if (kind is "Inherit")
        {
            Assert.Equal("Merge", (string?)account["permissions"]!["@type"]);
            Assert.Equal(["authenticate"], Members(account, "disabledPermissions"));
            Assert.Empty(Members(account, "enabledPermissions"));
        }
        else
        {
            Assert.Equal(kind, (string?)account["permissions"]!["@type"]);
            Assert.Equal(["authenticate", "emailReceive"], Members(account, "disabledPermissions"));
            Assert.Equal(["authenticate", "emailSend"], Members(account, "enabledPermissions"));
        }

        Assert.False(Assert.Single(Value(await started.Server.MailboxesAsync(cancellationToken))).Enabled);
    }

    /// <summary>
    /// INT-MAIL-006a AC3, INT-MAIL-007: the listing answers every user account the
    /// server holds, across its pages, with the identifier its description carries and
    /// whether it is enabled: <c>authenticate</c> not disabled and, under
    /// <c>Replace</c>, enabled. An account of another kind is no mailbox.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task INT_MAIL_006a_AC3_TheListingAnswersEnabledStateAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await using Started started = await StartedAsync();

        _ = _server.Account("inherits@example.test", Mailbox.ToString(), new JsonObject { ["@type"] = "Inherit" });
        _ = _server.Account("merges@example.test", Another.ToString(), new JsonObject
        {
            ["@type"] = "Merge",
            ["enabledPermissions"] = new JsonObject(),
            ["disabledPermissions"] = new JsonObject { ["authenticate"] = true },
        });
        _ = _server.Account("replaces@example.test", "made by hand", new JsonObject
        {
            ["@type"] = "Replace",
            ["enabledPermissions"] = new JsonObject { ["emailSend"] = true },
            ["disabledPermissions"] = new JsonObject(),
        });
        _ = _server.Account("granted@example.test", null, new JsonObject
        {
            ["@type"] = "Replace",
            ["enabledPermissions"] = new JsonObject { ["authenticate"] = true },
            ["disabledPermissions"] = new JsonObject(),
        });
        _ = _server.Account("team@example.test", null, new JsonObject { ["@type"] = "Inherit" }, kind: "Group");

        IReadOnlyList<HostedMailbox> listed = Value(await started.Server.MailboxesAsync(cancellationToken));

        Assert.Equal(
            [
                new HostedMailbox(Mailbox, "inherits@example.test", Enabled: true),
                new HostedMailbox(Another, "merges@example.test", Enabled: false),
                new HostedMailbox(null, "replaces@example.test", Enabled: false),
                new HostedMailbox(null, "granted@example.test", Enabled: true),
            ],
            listed);
        Assert.True(_server.Requests.Count > 1);
    }

    /// <summary>
    /// INT-MAIL-010 AC1: creating an app password is one call to the server's
    /// app-password call carrying the person's token and never the management key; the
    /// server generates the secret; the listing reads what the server holds; a revocation
    /// removes it there, and one of an app password the server does not hold is not
    /// found.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task INT_MAIL_010_AC1_AnAppPasswordIsOneCallCarryingThePersonsTokenAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await using Started started = await StartedAsync();
        var expiresAt = new DateTimeOffset(2027, 3, 1, 9, 30, 0, TimeSpan.Zero);

        IssuedAppPassword issued = Value(await started.Server.CreateAppPasswordAsync(
            PersonToken,
            "Phone",
            expiresAt,
            cancellationToken));

        JmapServerInMemory.Received created = Assert.Single(_server.Requests);
        JsonNode call = Assert.Single((JsonArray)created.Body!["methodCalls"]!)!;

        Assert.Equal(PersonToken, created.Bearer);
        Assert.Equal("x:AppPassword/set", (string?)call[0]);
        Assert.Equal("generated-" + issued.Id, issued.Secret);

        JsonObject held = Assert.Single(_server.PasswordsOf("ada"));

        Assert.Equal("Phone", (string?)held["description"]);
        Assert.Equal("2027-03-01T09:30:00Z", (string?)held["expiresAt"]);
        Assert.Equal("Inherit", (string?)held["permissions"]!["@type"]);

        Assert.Equal(
            [new AppPassword(issued.Id, "Phone", new DateTimeOffset(2026, 9, 30, 12, 0, 0, TimeSpan.Zero), expiresAt)],
            Value(await started.Server.AppPasswordsAsync(PersonToken, cancellationToken)));

        Assert.True(Succeeded(await started.Server.RevokeAppPasswordAsync(PersonToken, issued.Id, cancellationToken)));
        Assert.Empty(_server.PasswordsOf("ada"));
        Assert.Equal(
            ErrorCodes.CredentialNotFound,
            Refusal(await started.Server.RevokeAppPasswordAsync(PersonToken, issued.Id, cancellationToken)));
        Assert.All(_server.Requests, request => Assert.Equal(PersonToken, request.Bearer));
    }

    /// <summary>
    /// INT-MAIL-001: an answer that is not 2xx, that does not read, that is a method
    /// error, or that the server cannot give in time is a failure, never a success.
    /// </summary>
    /// <param name="answer">What the server answers.</param>
    /// <returns>The work of the test.</returns>
    [Theory]
    [InlineData("status")]
    [InlineData("unreadable")]
    [InlineData("error")]
    [InlineData("unreachable")]
    public async Task JmapMailServer_AnAnswerThatDoesNotRead_IsAFailureAsync(string answer)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await using Started started = await StartedAsync();

        _server.Answer = answer switch
        {
            "status" => () => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable),
            "unreadable" => () => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("<html>maintenance</html>", Encoding.UTF8, "text/html"),
            },
            "error" => () => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"methodResponses":[["error",{"type":"serverFail"},"0"],["error",{"type":"serverFail"},"1"]],"sessionState":"0"}""",
                    Encoding.UTF8,
                    "application/json"),
            },
            _ => () => throw new HttpRequestException("The mail server is unreachable."),
        };

        Assert.Equal(
            ErrorCodes.SystemFault,
            Refusal(await started.Server.ProvisionAsync(Push(MailboxState.Disabled), cancellationToken)));
        Assert.Equal(
            ErrorCodes.SystemFault,
            (await started.Server.MailboxesAsync(cancellationToken)).Match<ErrorCode?>(_ => null, error => error.Code));
        Assert.Equal(
            ErrorCodes.SystemFault,
            Refusal(await started.Server.RevokeAppPasswordAsync(PersonToken, "p0001", cancellationToken)));
    }

    /// <summary>
    /// CONV-DESIGN-007 AC5, D-176: with the endpoint set and no mail server of the host's,
    /// the start chooses the adapter and the ring holds its key, which every management
    /// call presents; a change of the endpoint after the start changes nothing.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task CONV_DESIGN_007_AC5_TheAdapterIsChosenWhereTheEndpointIsSetAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await using Started started = await StartedAsync();

        Assert.IsType<JmapMailServer>(started.Server);
        Assert.Contains("mailServerSecret", started.Secrets.Asked);

        _configuration.Set(Settings.IntegrationMailServerEndpoint, string.Empty);

        Assert.Same(started.Server, Chosen(started.Services));
        Assert.True(Succeeded(await Chosen(started.Services)!.ProvisionAsync(Push(MailboxState.Disabled), cancellationToken)));
        Assert.All(_server.Requests, request => Assert.Equal(_server.ManagementKey, request.Bearer));
    }

    /// <summary>
    /// CONV-DESIGN-007 AC5, D-176: with a mail server of the host's registered the start
    /// chooses it and never reads the adapter's key; with the endpoint empty and none
    /// registered it chooses none and reads no key.
    /// </summary>
    /// <param name="hosted">Whether the host registers a mail server of its own.</param>
    /// <returns>The work of the test.</returns>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CONV_DESIGN_007_AC5_TheAdapterIsNotChosenWhereTheHostsOrNoneIsAsync(bool hosted)
    {
        if (!hosted)
        {
            _configuration.Set(Settings.IntegrationMailServerEndpoint, string.Empty);
        }

        var host = new HostedServerInMemory();

        await using Started started = await StartedAsync(hosted ? host : null);

        Assert.DoesNotContain("mailServerSecret", started.Secrets.Asked);
        Assert.Same(hosted ? host : null, Chosen(started.Services));
        Assert.Empty(_server.Requests);
    }

    /// <summary>
    /// CONV-CODE-007 AC3, D-176: the mail server's key is lent only once the start has
    /// chosen the mail server in use; a call of the adapter before that is a fault.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task CONV_CODE_007_AC3_AReadOfTheMailServerKeyBeforeTheChoiceThrowsAsync()
    {
        await using ServiceProvider services = Services(host: null, new SecretSourceInMemory(NoCredentials)
        {
            MailServerSecret = Encoding.UTF8.GetBytes(_server.ManagementKey),
        });

        await services.GetRequiredService<KeyRingService>().StartingAsync(TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await services.GetRequiredService<JmapMailServer>().MailboxesAsync(TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// LIB-HOST-001, D-176: where the adapter is chosen and the secret source cannot
    /// answer the mail server's key, or answers it empty, the start stops, naming it.
    /// </summary>
    /// <param name="empty">Whether the source answers an empty key rather than none.</param>
    /// <returns>The work of the test.</returns>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LIB_HOST_001_AMailServerKeyThatCannotBeReadStopsTheStartAsync(bool empty)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await using ServiceProvider services = Services(host: null, new SecretSourceInMemory(NoCredentials)
        {
            MailServerSecret = empty ? [] : null,
        });
        KeyRingService ring = services.GetRequiredService<KeyRingService>();

        await ring.StartingAsync(cancellationToken);

        StartupException refused = await Assert.ThrowsAsync<StartupException>(() => ring.StartAsync(cancellationToken));

        Assert.Equal(ErrorCodes.StartupSecretUnavailable, refused.Failure?.Code);
        Assert.Equal("mailServerSecret", refused.Failure?.Details["key"].GetString());
    }

    /// <summary>
    /// INT-GEN-001 AC1: a mail server endpoint that is not an absolute https address
    /// stops the start, naming the key, and no key is read for it.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task INT_GEN_001_AC1_APlaintextMailServerEndpointStopsStartupAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        var secrets = new SecretSourceInMemory(NoCredentials) { MailServerSecret = [1, 2, 3] };

        _configuration.Set(Settings.IntegrationMailServerEndpoint, "http://mail.example.test");

        await using ServiceProvider services = Services(host: null, secrets);
        KeyRingService ring = services.GetRequiredService<KeyRingService>();

        await ring.StartingAsync(cancellationToken);

        StartupException refused = await Assert.ThrowsAsync<StartupException>(() => ring.StartAsync(cancellationToken));

        Assert.Equal(ErrorCodes.EndpointInsecure, refused.Failure?.Code);
        Assert.Equal("integration.mailserver.endpoint", refused.Failure?.Details["key"].GetString());
        Assert.DoesNotContain("mailServerSecret", secrets.Asked);
    }

    /// <inheritdoc/>
    public void Dispose() => _server.Dispose();

    private static MailboxPush Push(MailboxState state) =>
        new(Mailbox, Guid.CreateVersion7(), Address, state);

    private static bool Succeeded(Result result) => result.Match(() => true, _ => false);

    private static ErrorCode? Refusal(Result result) => result.Match<ErrorCode?>(() => null, error => error.Code);

    private static TValue Value<TValue>(Result<TValue> result) =>
        result.Match(value => value, error => throw new InvalidOperationException(error.Code.ToString()));

    private static string[] Members(JsonObject account, string set) =>
        account["permissions"]![set] is JsonObject members
            ? [.. members.Where(member => (bool?)member.Value == true).Select(member => member.Key).Order(StringComparer.Ordinal)]
            : [];

    private static IMailServer? Chosen(IServiceProvider services) =>
        services.GetRequiredService<IMailServerInUse>().Chosen().Match<IMailServer?>(chosen => chosen, _ => null);

    private ServiceProvider Services(IMailServer? host, SecretSourceInMemory secrets)
    {
        var services = new ServiceCollection();

        services.AddSingleton<IConfigurationStore>(_configuration);
        services.AddSingleton<ISecretSource>(secrets);
        services.AddKeyRing();
        services.AddSingleton<JmapMailServer>();
        _ = services.AddHttpClient(JmapMailServer.Channel).ConfigurePrimaryHttpMessageHandler(() => _server);
        services.AddSingleton<KeyRingService>();

        if (host is not null)
        {
            services.AddSingleton(host);
        }

        return services.BuildServiceProvider();
    }

    private async Task<Started> StartedAsync(IMailServer? host = null)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        var secrets = new SecretSourceInMemory(NoCredentials)
        {
            MailServerSecret = Encoding.UTF8.GetBytes(_server.ManagementKey),
        };
        ServiceProvider services = Services(host, secrets);
        KeyRingService ring = services.GetRequiredService<KeyRingService>();

        await ring.StartingAsync(cancellationToken);
        await ring.StartAsync(cancellationToken);

        return new Started(services, secrets, Chosen(services)!);
    }

    // A deployment started as far as the choice of its mail server.
    private sealed record Started(ServiceProvider Services, SecretSourceInMemory Secrets, IMailServer Server) : IAsyncDisposable
    {
        public ValueTask DisposeAsync() => Services.DisposeAsync();
    }

    // A mail server of the host's own, which the start chooses and nothing here calls.
    private sealed class HostedServerInMemory : IMailServer
    {
        public ValueTask<Result> ProvisionAsync(MailboxPush push, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("The host's mail server is not called here.");

        public ValueTask<Result<IReadOnlyList<HostedMailbox>>> MailboxesAsync(CancellationToken cancellationToken) =>
            throw new InvalidOperationException("The host's mail server is not called here.");

        public ValueTask<Result<IReadOnlyList<AppPassword>>> AppPasswordsAsync(
            string accessToken,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException("The host's mail server is not called here.");

        public ValueTask<Result<IssuedAppPassword>> CreateAppPasswordAsync(
            string accessToken,
            string label,
            DateTimeOffset? expiresAt,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException("The host's mail server is not called here.");

        public ValueTask<Result> RevokeAppPasswordAsync(
            string accessToken,
            string id,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException("The host's mail server is not called here.");
    }
}
