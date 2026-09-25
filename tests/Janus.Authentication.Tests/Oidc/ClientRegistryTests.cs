using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Janus.Authentication.Oidc;
using Janus.Core;
using Janus.Core.Configuration;
using Xunit;

namespace Janus.Authentication.Tests.Oidc;

/// <summary>
/// How a client comes to be in the registry: from the server, recorded under the
/// command's principal, with a replaced secret kept through the overlap and no longer
/// (AUTH-OIDC-001, OPS-SEC-002, entry 340).
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

    private static readonly byte[] First = Encoding.UTF8.GetBytes("the-first-secret-of-the-mail-server");

    private static readonly byte[] Second = Encoding.UTF8.GetBytes("the-second-secret-of-the-mail-server");

    private readonly OidcClientStoreInMemory _clients = new();
    private readonly OidcAuditInMemory _audit = new();
    private readonly ConfigurationInMemory _configuration = new();
    private readonly UnitOfWorkInMemory _work = new();
    private readonly FixedClock _clock = new(Noon);

    /// <inheritdoc/>
    public async ValueTask DisposeAsync() => await _work.DisposeAsync();

    /// <summary>
    /// AUTH-OIDC-001 AC4: the mail-server client is registered from the server, the
    /// registry holding what its secret hashes to and never the secret, and the
    /// registration is recorded under the command's principal in one transaction.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_OIDC_001_AC4_TheMailServerClientIsRegisteredFromTheServerAsync()
    {
        Assert.Null(Refused(await RegisteredAsync(Mail, First)));

        RegisteredClient held = Assert.Single(_clients.Registered);

        Assert.Equal(Mail, held.Client);
        Assert.Equal(SHA256.HashData(First), held.Secret);
        Assert.Null(held.Previous);
        Assert.Equal(
            [(ClientRegistry.Principal, "mail", OidcClientKind.Protocol, false, Noon)],
            _audit.Registrations);
        Assert.Equal((1, 1), (_work.Opened, _work.Committed));
    }

    /// <summary>
    /// OPS-SEC-002 AC2: a secret that replaces another leaves the one it replaced taken
    /// for the access-token lifetime and five minutes, and the change is recorded as one.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task OPS_SEC_002_AC2_AReplacedSecretIsKeptThroughTheOverlapAsync()
    {
        await RegisteredAsync(Mail, First);
        _clock.Advance(TimeSpan.FromDays(30));

        Assert.Null(Refused(await RegisteredAsync(Mail, Second)));

        RegisteredClient held = Assert.Single(_clients.Registered);

        Assert.Equal(SHA256.HashData(Second), held.Secret);
        Assert.Equal(SHA256.HashData(First), held.Previous);
        Assert.Equal(Noon + TimeSpan.FromDays(30) + TimeSpan.FromMinutes(15), held.PreviousUntil);
        Assert.True(_audit.Registrations[^1].Changed);
    }

    /// <summary>
    /// OPS-SEC-002 AC2: the overlap follows the access-token lifetime the deployment
    /// set, as the signing keys' does.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task OPS_SEC_002_AC2_TheOverlapFollowsTheAccessTokenLifetimeAsync()
    {
        _configuration.Set(Settings.OidcAccessTokenLifetime, TimeSpan.FromMinutes(30));

        await RegisteredAsync(Mail, First);
        await RegisteredAsync(Mail, Second);

        Assert.Equal(Noon + TimeSpan.FromMinutes(35), Assert.Single(_clients.Registered).PreviousUntil);
    }

    /// <summary>
    /// OPS-SEC-002: a change that keeps the secret replaces nothing, so it neither
    /// extends the overlap of a secret replaced before nor admits the current one twice.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task OPS_SEC_002_AChangeThatKeepsTheSecretReplacesNothingAsync()
    {
        await RegisteredAsync(Mail, First);
        await RegisteredAsync(Mail, Second);
        _clock.Advance(TimeSpan.FromDays(1));

        Assert.Null(Refused(await RegisteredAsync(Mail with { Name = "Mail" }, Second)));

        RegisteredClient held = Assert.Single(_clients.Registered);

        Assert.Equal("Mail", held.Client.Name);
        Assert.Equal(SHA256.HashData(First), held.Previous);
        Assert.Equal(Noon + TimeSpan.FromMinutes(15), held.PreviousUntil);
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
    [InlineData("redirect")]
    [InlineData("scopes")]
    public async Task AUTH_OIDC_001_AClientTheRegistryCannotServeIsRefusedAsync(string member)
    {
        OidcClient broken = member switch
        {
            "client" => Mail with { ClientId = "mail server" },
            "name" => Mail with { Name = " " },
            "redirect" => Mail with { Redirect = "/callback" },
            _ => Mail with { Scopes = ["openid", string.Empty] },
        };

        Assert.Equal(member, Refused(await RegisteredAsync(broken, First)));
        Assert.Empty(_clients.Registered);
        Assert.Empty(_audit.Registrations);
        Assert.Equal(0, _work.Opened);
    }

    /// <summary>
    /// OPS-SEC-001: a secret shorter than a key of the deployment, not text, or nothing
    /// but white space is refused, since the server would never take it or a search over
    /// the registry's hashes would reach it.
    /// </summary>
    /// <param name="secret">The secret, in base64.</param>
    /// <returns>The work of the test.</returns>
    [Theory]
    [InlineData("c2hvcnQtc2VjcmV0")]
    [InlineData("//////////////////////////////////////////8=")]
    [InlineData("ICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICA=")]
    public async Task OPS_SEC_001_ASecretTheServerWouldNotTakeIsRefusedAsync(string secret)
    {
        Assert.Equal("clientSecret", Refused(await RegisteredAsync(Mail, Convert.FromBase64String(secret))));
        Assert.Empty(_clients.Registered);
    }

    private static string? Refused(Result result) =>
        result.Match(
            () => null,
            failure =>
            {
                Assert.Equal(ErrorCodes.RequestMalformed, failure.Code);

                return failure.Details["member"].GetString();
            });

    private async Task<Result> RegisteredAsync(OidcClient client, byte[] secret) =>
        await new ClientRegistry(_clients, _audit, _configuration, _work, _clock)
            .RegisterAsync(client, secret, TestContext.Current.CancellationToken);
}
