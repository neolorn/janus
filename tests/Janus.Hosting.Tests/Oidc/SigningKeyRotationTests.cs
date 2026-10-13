using System;
using System.Buffers.Text;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Janus.Authentication.Oidc;
using Janus.Core.Configuration;
using Janus.Storage.Authentication.Oidc;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Server;
using Xunit;

namespace Janus.Hosting.Tests.Oidc;

/// <summary>
/// The signing keys as the provider reads them while it runs: what it signs with, what
/// its key set publishes, what its validation accepts, and what its options hold, across
/// rotations nobody asked for (AUTH-KEY-001, CONV-CODE-007).
/// </summary>
[Trait("kind", "unit")]
public sealed class SigningKeyRotationTests
{
    // A cadence short enough that the session the cases sign in with is still live
    // when a key it was issued under is retired.
    private static readonly TimeSpan Cadence = TimeSpan.FromDays(1);

    private static readonly TimeSpan Lead = TimeSpan.FromMinutes(5);

    // The access-token lifetime, 10 minutes by default, and the margin of 5.
    private static readonly TimeSpan Overlap = TimeSpan.FromMinutes(15);

    private static readonly TimeSpan Second = TimeSpan.FromSeconds(1);

    /// <summary>
    /// AUTH-KEY-001 AC1: the running provider signs with the next key once the cadence
    /// has passed and the next key has been published for five minutes, with nobody
    /// asked and nothing restarted.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_KEY_001_AC1_TheRunningProviderSignsWithTheNextKeyAfterTheCadenceAsync()
    {
        await using Deployment deployment = Rotating();

        Browser browser = await RelyingParty.PreparedAsync(deployment);
        string first = Header(await AccessTokenAsync(deployment, browser), "kid");

        deployment.Clock.Advance(Cadence - Lead);

        IReadOnlyList<string> published = await PublishedAsync(deployment);

        deployment.Clock.Advance(Lead);

        string second = Header(await AccessTokenAsync(deployment, browser), "kid");

        Assert.Equal(2, published.Count);
        Assert.NotEqual(first, second);
        Assert.Contains(second, published);
    }

    /// <summary>
    /// AUTH-KEY-001 AC2: before the provider signs an access token under a longer
    /// lifetime than the current key carries, the longer one is stored with the key.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_KEY_001_AC2_TheLongestLifetimeIsStoredWithTheKeyThatSignsAsync()
    {
        await using Deployment deployment = Rotating();

        deployment.Configuration.Set(Settings.OidcAccessTokenLifetime, TimeSpan.FromMinutes(30));

        Browser browser = await RelyingParty.PreparedAsync(deployment);
        string signed = Header(await AccessTokenAsync(deployment, browser), "kid");

        Assert.Equal(TimeSpan.FromMinutes(30), deployment.Keys.Held(signed)!.LongestLifetime);
    }

    /// <summary>
    /// AUTH-KEY-001 AC2 and AC3: an access token the previous key signed is accepted
    /// through the overlap; once it ends, the key leaves the key set, which is still
    /// answered, and the provider's validation refuses the token though it has not
    /// expired.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_KEY_001_AC3_AfterTheOverlapTheKeyLeavesTheSetAndItsAccessTokenIsRefusedAsync()
    {
        await using Deployment deployment = Rotating();

        Browser browser = await RelyingParty.PreparedAsync(deployment);
        string issued = await AccessTokenAsync(deployment, browser);
        string first = Header(issued, "kid");
        string outliving = await OutlivingAsync(deployment, issued);
        var machine = new Machine(deployment);

        await RotatedAsync(deployment);
        deployment.Clock.Advance(Overlap - Second);

        Assert.Contains(first, await PublishedAsync(deployment));
        Assert.Equal(StatusCodes.Status200OK, (await machine.GetAsync("/oidc/userinfo", outliving)).Status);

        deployment.Clock.Advance(Second);

        Answer set = await machine.GetAsync("/oidc/jwks", bearer: string.Empty);

        Assert.Equal(StatusCodes.Status200OK, set.Status);
        Assert.DoesNotContain(first, Kids(set));
        Assert.Equal(
            StatusCodes.Status401Unauthorized,
            (await machine.GetAsync("/oidc/userinfo", outliving)).Status);
    }

    /// <summary>
    /// AUTH-KEY-001 AC3: the discovery document and the hashes the identity token
    /// carries are unchanged once the key the provider started with is retired: the one
    /// algorithm is still advertised, and <c>at_hash</c> is still the left half of the
    /// SHA-256 of the access token, so the steps that stand in the server's own read
    /// the credential source and not the disposed object its options hold.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_KEY_001_AC3_TheDocumentAndTheTokenHashesAreUnchangedAfterTheStartKeyRetiresAsync()
    {
        await using Deployment deployment = Rotating();

        Browser browser = await RelyingParty.PreparedAsync(deployment);
        var machine = new Machine(deployment);

        (string access, string identity) before = await ExchangedAsync(deployment, browser);

        await RotatedAsync(deployment);
        deployment.Clock.Advance(Overlap);
        _ = await PublishedAsync(deployment);

        (string access, string identity) after = await ExchangedAsync(deployment, browser);
        JsonElement document = (await machine.GetAsync("/.well-known/openid-configuration", bearer: string.Empty))
            .Json();

        Assert.Equal(
            ["ES256"],
            document.GetProperty("id_token_signing_alg_values_supported").EnumerateArray().Select(held => held.GetString()));
        Assert.Equal(LeftHalf(before.access), Claim(before.identity, "at_hash"));
        Assert.Equal(LeftHalf(after.access), Claim(after.identity, "at_hash"));
        Assert.NotEqual(Header(before.identity, "kid"), Header(after.identity, "kid"));
    }

    /// <summary>
    /// AUTH-KEY-001 AC4: every token the provider issues is signed with
    /// <c>token.signing.algorithm</c>: the access token, the identity token, and the
    /// refresh token, whose signature is under its encryption.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_KEY_001_AC4_EveryIssuedTokenIsSignedWithTheConfiguredAlgorithmAsync()
    {
        await using Deployment deployment = Rotating();

        Browser browser = await RelyingParty.PreparedAsync(deployment);
        string code = await RelyingParty.CodeAsync(deployment, browser, RelyingParty.Protocol);
        Answer exchanged = await new Machine(deployment).PostAsync(
            "/oidc/token",
            RelyingParty.Code(code, RelyingParty.Protocol));

        Assert.Equal("ES256", Header(exchanged.Text("access_token"), "alg"));
        Assert.Equal("ES256", Header(exchanged.Text("id_token"), "alg"));
        Assert.Equal("ES256", Decrypted(deployment, exchanged.Text("refresh_token")).Alg);
    }

    /// <summary>
    /// AUTH-KEY-001 AC5, CONV-CODE-007 AC4: the provider's options are built once, at
    /// the start, with the object the source holds; after a rotation and a retirement
    /// they are the same options, holding that same object, disposed, and the provider
    /// signs with the current key all the same.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task CONV_CODE_007_AC4_TheOptionsAreNeverRebuiltAndHoldOnlyTheSourcesObjectAsync()
    {
        await using Deployment deployment = Rotating();

        OpenIddictServerOptions built;
        SigningCredentials started;

        await using (AsyncServiceScope scope = deployment.Scope())
        {
            built = scope.ServiceProvider.GetRequiredService<IOptionsMonitor<OpenIddictServerOptions>>().CurrentValue;
            started = scope.ServiceProvider.GetRequiredService<SigningCredentialSource>().Started;
        }

        Assert.Same(started, Assert.Single(built.SigningCredentials));

        Browser browser = await RelyingParty.PreparedAsync(deployment);

        _ = await AccessTokenAsync(deployment, browser);
        await RotatedAsync(deployment);
        deployment.Clock.Advance(Overlap);
        _ = await PublishedAsync(deployment);

        string signed = Header(await AccessTokenAsync(deployment, browser), "kid");

        await using (AsyncServiceScope scope = deployment.Scope())
        {
            OpenIddictServerOptions read =
                scope.ServiceProvider.GetRequiredService<IOptionsMonitor<OpenIddictServerOptions>>().CurrentValue;

            Assert.Same(built, read);
            Assert.Same(started, Assert.Single(read.SigningCredentials));
        }

        Assert.False(deployment.Keys.HoldsPrivateKey(started.Key.KeyId));
        Assert.Throws<ObjectDisposedException>(() =>
            ((ECDsaSecurityKey)started.Key).ECDsa.SignData([1, 2, 3], HashAlgorithmName.SHA256));
        Assert.NotEqual(started.Key.KeyId, signed);
    }

    /// <summary>
    /// AUTH-KEY-001 AC6: a refresh token a retired key signed is accepted while its
    /// session lives, and a consumed one presented again revokes its family, the token
    /// the current key signed after it included.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_KEY_001_AC6_ARefreshTokenARetiredKeySignedIsStillAcceptedAsync()
    {
        await using Deployment deployment = Rotating();

        Browser browser = await RelyingParty.PreparedAsync(deployment);
        var machine = new Machine(deployment);
        string code = await RelyingParty.CodeAsync(deployment, browser, RelyingParty.Protocol);
        Answer exchanged = await machine.PostAsync("/oidc/token", RelyingParty.Code(code, RelyingParty.Protocol));
        string first = exchanged.Text("refresh_token");
        string retired = Header(exchanged.Text("access_token"), "kid");
        string second = (await machine.PostAsync("/oidc/token", Refresh(first))).Text("refresh_token");

        await RotatedAsync(deployment);
        deployment.Clock.Advance(Overlap);

        Assert.DoesNotContain(retired, await PublishedAsync(deployment));
        Assert.False(deployment.Keys.HoldsPrivateKey(retired));

        Answer refreshed = await machine.PostAsync("/oidc/token", Refresh(second));

        Assert.Equal(StatusCodes.Status200OK, refreshed.Status);
        Assert.NotEqual(retired, Header(refreshed.Text("access_token"), "kid"));

        Answer replayed = await machine.PostAsync("/oidc/token", Refresh(first));
        Answer revoked = await machine.PostAsync("/oidc/token", Refresh(refreshed.Text("refresh_token")));

        Assert.Equal(StatusCodes.Status400BadRequest, replayed.Status);
        Assert.Equal(StatusCodes.Status400BadRequest, revoked.Status);
        Assert.NotEmpty(deployment.OidcAudit.Reuses);
    }

    /// <summary>
    /// AUTH-KEY-001 AC7: the deployment's start makes a key current before the
    /// provider's options are built, and the options are built with it.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_KEY_001_AC7_TheStartMakesAKeyCurrentBeforeTheOptionsAreBuiltAsync()
    {
        await using Deployment deployment = Rotating();
        await using AsyncServiceScope scope = deployment.Scope();

        SigningCredentials built = Assert.Single(
            scope.ServiceProvider.GetRequiredService<IOptionsMonitor<OpenIddictServerOptions>>()
                .CurrentValue
                .SigningCredentials);

        Assert.Equal(1, deployment.Keys.Count);
        Assert.True(deployment.Keys.Held(built.Key.KeyId)!.IsCurrent);
    }

    /// <summary>
    /// AUTH-KEY-001 AC8: a key the first read after the cadence makes is published
    /// before any token carries it, and signs only once it has been published five
    /// minutes.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_KEY_001_AC8_TheNextKeyIsPublishedFiveMinutesBeforeItSignsAsync()
    {
        await using Deployment deployment = Rotating();

        Browser browser = await RelyingParty.PreparedAsync(deployment);
        string first = Header(await AccessTokenAsync(deployment, browser), "kid");

        deployment.Clock.Advance(Cadence + TimeSpan.FromHours(1));

        string still = Header(await AccessTokenAsync(deployment, browser), "kid");
        string next = Assert.Single(await PublishedAsync(deployment), keyId => keyId != first);

        deployment.Clock.Advance(Lead - Second);

        string before = Header(await AccessTokenAsync(deployment, browser), "kid");

        deployment.Clock.Advance(Second);

        string after = Header(await AccessTokenAsync(deployment, browser), "kid");

        Assert.Equal(first, still);
        Assert.Equal(first, before);
        Assert.Equal(next, after);
    }

    private static Deployment Rotating()
    {
        var deployment = new Deployment();

        deployment.Configuration.Set(Settings.TokenSigningRotation, Cadence);

        return deployment;
    }

    // The next key made by the first read at the cadence less five minutes, and made
    // current by the first read five minutes later: here, two requests for the key set.
    private static async Task RotatedAsync(Deployment deployment)
    {
        deployment.Clock.Advance(Cadence - Lead);
        _ = await PublishedAsync(deployment);
        deployment.Clock.Advance(Lead);
        _ = await PublishedAsync(deployment);
    }

    private static async Task<IReadOnlyList<string>> PublishedAsync(Deployment deployment) =>
        Kids(await new Machine(deployment).GetAsync("/oidc/jwks", bearer: string.Empty));

    private static List<string> Kids(Answer answered) =>
        [.. answered.Json().GetProperty("keys").EnumerateArray().Select(key => key.GetProperty("kid").GetString()!)];

    private static async Task<string> AccessTokenAsync(Deployment deployment, Browser browser) =>
        (await ExchangedAsync(deployment, browser)).Access;

    private static async Task<(string Access, string Identity)> ExchangedAsync(Deployment deployment, Browser browser)
    {
        string code = await RelyingParty.CodeAsync(deployment, browser, RelyingParty.Application);
        Answer exchanged = await new Machine(deployment).PostAsync(
            "/oidc/token",
            RelyingParty.Code(code, RelyingParty.Application));

        Assert.Equal(StatusCodes.Status200OK, exchanged.Status);

        return (exchanged.Text("access_token"), exchanged.Text("id_token"));
    }

    // The same access token, its entry and the token itself expiring two days later, signed
    // again with the key that signed it: what outlives the overlap is refused only for
    // the key it names.
    private static async Task<string> OutlivingAsync(Deployment deployment, string issued)
    {
        string keyId = Header(issued, "kid");
        byte[] privateKey = (await deployment.Keys.PrivateKeyAsync(keyId, TestContext.Current.CancellationToken))!;
        JsonObject payload = JsonNode.Parse(Base64Url.DecodeFromChars(issued.Split('.')[1]))!.AsObject();
        OidcTokenRecord entry = (await deployment.Tokens.FindByIdAsync(
            payload["oi_tkn_id"]!.GetValue<string>(),
            TestContext.Current.CancellationToken))!;

        await deployment.Tokens.SetExpirationDateAsync(
            entry,
            entry.ExpiresAt + TimeSpan.FromDays(2),
            TestContext.Current.CancellationToken);

        payload["exp"] = payload["exp"]!.GetValue<long>() + (long)TimeSpan.FromDays(2).TotalSeconds;

        using var signer = ECDsa.Create();

        signer.ImportPkcs8PrivateKey(privateKey, out _);
        CryptographicOperations.ZeroMemory(privateKey);

        return new JsonWebTokenHandler().CreateToken(
            payload.ToJsonString(),
            new SigningCredentials(new ECDsaSecurityKey(signer) { KeyId = keyId }, SecurityAlgorithms.EcdsaSha256),
            new Dictionary<string, object>(StringComparer.Ordinal) { ["typ"] = Header(issued, "typ") });
    }

    private static JsonWebToken Decrypted(Deployment deployment, string token)
    {
        using AsyncServiceScope scope = deployment.Scope();

        OpenIddictServerOptions options =
            scope.ServiceProvider.GetRequiredService<IOptionsMonitor<OpenIddictServerOptions>>().CurrentValue;

        var handler = new JsonWebTokenHandler();
        string inner = handler.DecryptToken(
            new JsonWebToken(token),
            new TokenValidationParameters
            {
                TokenDecryptionKeys = [.. options.EncryptionCredentials.Select(credentials => credentials.Key)],
            });

        return new JsonWebToken(inner);
    }

    private static string LeftHalf(string token)
    {
        byte[] digest = SHA256.HashData(Encoding.ASCII.GetBytes(token));

        return Base64Url.EncodeToString(digest.AsSpan(0, digest.Length / 2));
    }

    private static (string Name, string? Value)[] Refresh(string token) =>
    [
        ("grant_type", "refresh_token"),
        ("refresh_token", token),
        ("client_id", RelyingParty.Protocol),
        ("client_secret", RelyingParty.Secret),
    ];

    private static string Header(string token, string name) =>
        JsonDocument
            .Parse(Base64Url.DecodeFromChars(token.Split('.')[0]))
            .RootElement
            .GetProperty(name)
            .GetString() ?? string.Empty;

    private static string Claim(string token, string name) =>
        JsonDocument
            .Parse(Base64Url.DecodeFromChars(token.Split('.')[1]))
            .RootElement
            .GetProperty(name)
            .GetString() ?? string.Empty;
}
