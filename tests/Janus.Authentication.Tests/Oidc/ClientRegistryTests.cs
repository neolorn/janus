using System;
using System.Buffers.Text;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Janus.Authentication.Oidc;
using Janus.Core;
using Xunit;

namespace Janus.Authentication.Tests.Oidc;

/// <summary>
/// How a client comes to be in the registry: from the server, recorded under the
/// command's principal, with a secret the library draws and no person supplies, which a
/// change to the client leaves as it stands (AUTH-OIDC-001, OPS-SEC-002, D-166 340).
/// </summary>
[Trait("kind", "unit")]
public sealed class ClientRegistryTests : IAsyncDisposable
{
    private static readonly DateTimeOffset Noon = new(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);

    private static readonly OidcClient Mail = new(
        "mail",
        "Mail server",
        OidcClientKind.Protocol,
        "https://mail.example.test/callback",
        ["openid", "email", "offline_access"]);

    private readonly OidcClientStoreInMemory _clients = new();
    private readonly OidcAuditInMemory _audit = new();
    private readonly UnitOfWorkInMemory _work = new();
    private readonly FixedClock _clock = new(Noon);

    /// <inheritdoc/>
    public async ValueTask DisposeAsync() => await _work.DisposeAsync();

    /// <summary>
    /// AUTH-OIDC-001 AC4, OPS-SEC-002: the mail-server client is registered from the
    /// server with a secret the library draws, thirty-two bytes written base64url, issued
    /// now, and the registration is recorded under the command's principal in one
    /// transaction.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_OIDC_001_AC4_TheMailServerClientIsRegisteredFromTheServerAsync()
    {
        Assert.Null(Refused(await RegisteredAsync(Mail)));

        RegisteredClient held = Assert.Single(_clients.Registered);

        Assert.Equal(Mail, held.Client);
        Assert.Equal(32, Base64Url.DecodeFromUtf8(held.Secret).Length);
        Assert.Equal(Noon, held.IssuedAt);
        Assert.Null(held.Previous);
        Assert.Equal(
            [(ClientRegistry.Principal, "mail", OidcClientKind.Protocol, false, Noon)],
            _audit.Registrations);
        Assert.Equal((1, 1), (_work.Opened, _work.Committed));
    }

    /// <summary>
    /// OPS-SEC-002: two clients are never registered with one secret, since each is
    /// drawn afresh.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task OPS_SEC_002_EachClientIsRegisteredWithASecretOfItsOwnAsync()
    {
        await RegisteredAsync(Mail);
        await RegisteredAsync(Mail with { ClientId = "calendar" });

        Assert.NotEqual(_clients.Registered[0].Secret, _clients.Registered[1].Secret);
    }

    /// <summary>
    /// OPS-SEC-002: registering a client the registry holds changes its name, kind,
    /// destination and scopes, leaves its secret and when it was issued as they stand,
    /// and is recorded as a change.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task OPS_SEC_002_AChangeToAClientLeavesItsSecretAsync()
    {
        await RegisteredAsync(Mail);

        byte[] drawn = Assert.Single(_clients.Registered).Secret;

        _clock.Advance(TimeSpan.FromDays(1));

        OidcClient changed = Mail with
        {
            Name = "Mail",
            Kind = OidcClientKind.BrowserApplication,
            Redirect = "https://mail.example.test/elsewhere",
            Scopes = ["openid"],
        };

        Assert.Null(Refused(await RegisteredAsync(changed)));

        RegisteredClient held = Assert.Single(_clients.Registered);

        Assert.Equal(changed, held.Client);
        Assert.Equal(drawn, held.Secret);
        Assert.Equal(Noon, held.IssuedAt);
        Assert.Null(held.Previous);
        Assert.True(_audit.Registrations[^1].Changed);
    }

    /// <summary>
    /// AUTH-OIDC-001 and API-REDIR-001: a client the registry could not serve is refused
    /// naming the member, and nothing is registered or recorded.
    /// </summary>
    /// <param name="member">The member the case breaks.</param>
    /// <returns>The work of the test.</returns>
    [Theory]
    [InlineData("client")]
    [InlineData("name")]
    [InlineData("scopes")]
    public async Task AUTH_OIDC_001_AClientTheRegistryCannotServeIsRefusedAsync(string member)
    {
        OidcClient broken = member switch
        {
            "client" => Mail with { ClientId = "mail server" },
            "name" => Mail with { Name = " " },
            _ => Mail with { Scopes = ["openid", string.Empty] },
        };

        Assert.Equal(member, Refused(await RegisteredAsync(broken)));
        Assert.Empty(_clients.Registered);
        Assert.Empty(_audit.Registrations);
        Assert.Equal(0, _work.Opened);
    }

    /// <summary>
    /// AUTH-OIDC-006, API-REDIR-001 AC3 (D-166, 145): a return address startup would
    /// refuse is refused where it is registered, as startup refuses it, naming the
    /// client, and nothing is registered or recorded.
    /// </summary>
    /// <param name="redirect">The return address.</param>
    /// <returns>The work of the test.</returns>
    [Theory]
    [InlineData("/callback")]
    [InlineData("http://mail.example.test/callback")]
    public async Task AUTH_OIDC_006_AReturnAddressStartupWouldRefuseIsNotRegisteredAsync(string redirect)
    {
        Error refused = (await RegisteredAsync(Mail with { Redirect = redirect }))
            .Match(() => throw new InvalidOperationException("The client was registered."), failure => failure);

        Assert.Equal(
            (ErrorCodes.StartupRedirectClient, Mail.ClientId),
            (refused.Code, refused.Details["client"].GetString()));
        Assert.Empty(_clients.Registered);
        Assert.Empty(_audit.Registrations);
        Assert.Equal(0, _work.Opened);
    }

    private static string? Refused(Result result) =>
        result.Match(
            () => null,
            failure =>
            {
                Assert.Equal(ErrorCodes.RequestMalformed, failure.Code);

                return failure.Details["member"].GetString();
            });

    private async Task<Result> RegisteredAsync(OidcClient client)
    {
        using var randomness = RandomNumberGenerator.Create();

        return await new ClientRegistry(_clients, _audit, _work, randomness, _clock)
            .RegisterAsync(client, TestContext.Current.CancellationToken);
    }
}
