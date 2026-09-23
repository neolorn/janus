using System;
using System.Collections.Immutable;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Dapper;
using Janus.Authentication;
using Janus.Authentication.Factors;
using Janus.Authentication.Oidc;
using Janus.Authentication.Sessions;
using Janus.Core;
using Janus.Storage.Authentication.Oidc;
using Janus.Storage.Authentication.Sessions;
using Npgsql;
using OpenIddict.Abstractions;
using Xunit;

namespace Janus.Storage.Tests.Authentication;

/// <summary>
/// What the provider keeps: the registry a client is read from, the grants and the
/// tokens the protocol server writes through the library's own stores, the signing key
/// whose private half is wrapped, and the sweep that takes what can no longer be
/// presented (AUTH-OIDC-001, AUTH-OIDC-002, AUTH-OIDC-003, AUTH-KEY-001, AUTH-KEY-002,
/// AUTH-KEY-003).
/// </summary>
[Trait("kind", "integration")]
public sealed class OidcStoreTests(DatabaseFixture database)
    : IClassFixture<DatabaseFixture>, IDisposable
{
    private const string ClientId = "mail-server";
    private const string Browser = "browser-app";
    private const string Destination = "https://mail.example.test/signin/callback";
    private const string Secret = "a-secret-the-deployment-set";

    private static readonly DateTimeOffset Noon = new(2026, 9, 19, 12, 0, 0, TimeSpan.Zero);

    private readonly Deployment _deployment = new(database);

    /// <summary>
    /// AUTH-OIDC-001 AC2: the registry holds what the secret hashes to, the column
    /// holds nothing the secret could be read out of, and what the protocol server is
    /// handed to compare against is that same fingerprint.
    /// </summary>
    [Fact]
    public async Task AUTH_OIDC_001_AC2_TheRegistryHoldsWhatTheSecretHashesToAsync()
    {
        await RegisteredAsync(ClientId, OidcClientKind.Protocol);

        await using NpgsqlConnection connection = await database.OpenAsync();

        byte[] stored = await connection.QuerySingleAsync<byte[]>(
            "SELECT secret FROM janus.oidc_clients WHERE client_id = @clientId",
            new { clientId = ClientId });

        Assert.Equal(OpaqueToken.Of(Secret).Fingerprint(), stored);

        await using JanusDbContext reading = database.Context();

        var applications = new OidcApplicationStore(reading);
        OidcClientRecord? held = await applications.FindByClientIdAsync(
            ClientId,
            TestContext.Current.CancellationToken);

        Assert.NotNull(held);
        Assert.Equal(
            Convert.ToBase64String(OpaqueToken.Of(Secret).Fingerprint()),
            await applications.GetClientSecretAsync(held, TestContext.Current.CancellationToken));
        Assert.Equal(
            ImmutableArray.Create(Destination),
            await applications.GetRedirectUrisAsync(held, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// AUTH-OIDC-001 AC3: the registry is the deployment's, so a request that reached
    /// the store is refused rather than kept.
    /// </summary>
    [Fact]
    public async Task AUTH_OIDC_001_AC3_NoRequestWritesTheRegistryAsync()
    {
        await RegisteredAsync(ClientId, OidcClientKind.Protocol);

        await using JanusDbContext reading = database.Context();

        var applications = new OidcApplicationStore(reading);
        OidcClientRecord held = (await applications.FindByClientIdAsync(
            ClientId,
            TestContext.Current.CancellationToken))!;

        _ = await Assert.ThrowsAsync<NotSupportedException>(async () =>
            await applications.InstantiateAsync(TestContext.Current.CancellationToken));
        _ = await Assert.ThrowsAsync<NotSupportedException>(async () =>
            await applications.SetRedirectUrisAsync(
                held,
                ["https://attacker.test/collect"],
                TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// AUTH-OIDC-002 AC1 and AC2, chapter 09 section 9: the grant that refreshes a
    /// token is a protocol client's and a browser application's own layer does not
    /// hold it.
    /// </summary>
    [Fact]
    public async Task AUTH_OIDC_002_AC1_OnlyAProtocolClientMayRefreshAsync()
    {
        await RegisteredAsync(ClientId, OidcClientKind.Protocol);
        await RegisteredAsync(Browser, OidcClientKind.BrowserApplication);

        await using JanusDbContext reading = database.Context();

        var applications = new OidcApplicationStore(reading);

        Assert.Contains(
            OpenIddictConstants.Permissions.GrantTypes.RefreshToken,
            await PermittedAsync(applications, ClientId));
        Assert.DoesNotContain(
            OpenIddictConstants.Permissions.GrantTypes.RefreshToken,
            await PermittedAsync(applications, Browser));
    }

    /// <summary>
    /// AUTH-KEY-002: the private half of a signing key is at rest under the
    /// key-encryption key, with the version that wrapped it beside it, and is readable
    /// again only through the store.
    /// </summary>
    [Fact]
    public async Task AUTH_KEY_002_ThePrivateHalfIsWrappedUnderTheKeyEncryptionKeyAsync()
    {
        using var created = ECDsa.Create(ECCurve.NamedCurves.nistP256);

        byte[] privateKey = created.ExportPkcs8PrivateKey();
        var key = SigningKey.Create("the-key", "ES256", created.ExportSubjectPublicKeyInfo(), Noon);

        await using (JanusDbContext writing = database.Context())
        {
            await Keys(writing).AddAsync(key, privateKey, TestContext.Current.CancellationToken);
            await writing.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using NpgsqlConnection connection = await database.OpenAsync();

        (byte[] Stored, int Version) held = await connection.QuerySingleAsync<(byte[], int)>(
            "SELECT private_key, key_version FROM janus.signing_keys WHERE key_id = @keyId",
            new { keyId = key.KeyId });

        Assert.NotEqual(privateKey, held.Stored);
        Assert.Equal(_deployment.Keys.CurrentVersion, held.Version);

        await using JanusDbContext reading = database.Context();

        Assert.Equal(
            privateKey,
            await Keys(reading).PrivateKeyAsync(key.KeyId, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// AUTH-KEY-003 AC1: a row that can no longer be presented is taken by the sweep,
    /// which is one call and no person's task; a redeemed row inside the window is
    /// left where it is, because that is what catches the second presentation.
    /// </summary>
    [Fact]
    public async Task AUTH_KEY_003_AC1_TheSweepTakesTheTokensThatCanNoLongerBePresentedAsync()
    {
        SubjectId subject = await SignedInAsync();

        await RegisteredAsync(ClientId, OidcClientKind.Protocol);

        Guid grant = await GrantedAsync(subject);
        Guid gone = await TokenAsync(subject, grant, Noon, Noon + TimeSpan.FromMinutes(1));
        Guid live = await TokenAsync(subject, grant, Noon, Noon + TimeSpan.FromDays(7));
        Guid spent = await TokenAsync(
            subject,
            grant,
            Noon,
            Noon + TimeSpan.FromDays(7),
            OpenIddictConstants.Statuses.Redeemed);
        Guid recent = await TokenAsync(
            subject,
            grant,
            Noon + TimeSpan.FromMinutes(10),
            Noon + TimeSpan.FromDays(7),
            OpenIddictConstants.Statuses.Redeemed);

        await using (JanusDbContext sweeping = database.Context())
        {
            Assert.True(
                await new OidcTokenStore(sweeping).PruneAsync(
                    Noon + TimeSpan.FromMinutes(5),
                    TestContext.Current.CancellationToken) >= 2);
        }

        await using JanusDbContext reading = database.Context();

        var tokens = new OidcTokenStore(reading);

        Assert.Null(await FoundAsync(tokens, gone));
        Assert.Null(await FoundAsync(tokens, spent));
        Assert.NotNull(await FoundAsync(tokens, live));
        Assert.NotNull(await FoundAsync(tokens, recent));
    }

    /// <summary>
    /// AUTH-OIDC-003 AC1: a reuse takes every token issued under the grant, and leaves
    /// the tokens of another grant exactly where they were.
    /// </summary>
    [Fact]
    public async Task AUTH_OIDC_003_AC1_RevokingAGrantTakesEveryTokenUnderItAsync()
    {
        SubjectId subject = await SignedInAsync();

        await RegisteredAsync(ClientId, OidcClientKind.Protocol);

        Guid reused = await GrantedAsync(subject);
        Guid other = await GrantedAsync(subject);
        Guid first = await TokenAsync(subject, reused, Noon, Noon + TimeSpan.FromDays(7));
        Guid second = await TokenAsync(subject, reused, Noon, Noon + TimeSpan.FromDays(7));
        Guid apart = await TokenAsync(subject, other, Noon, Noon + TimeSpan.FromDays(7));

        await using (JanusDbContext revoking = database.Context())
        {
            Assert.Equal(
                2,
                await new OidcTokenStore(revoking).RevokeByAuthorizationIdAsync(
                    reused.ToString(),
                    TestContext.Current.CancellationToken));
        }

        await using JanusDbContext reading = database.Context();

        var tokens = new OidcTokenStore(reading);

        Assert.Equal(OpenIddictConstants.Statuses.Revoked, (await FoundAsync(tokens, first))!.Status);
        Assert.Equal(OpenIddictConstants.Statuses.Revoked, (await FoundAsync(tokens, second))!.Status);
        Assert.Equal(OpenIddictConstants.Statuses.Valid, (await FoundAsync(tokens, apart))!.Status);
    }

    /// <summary>
    /// AUTH-OIDC-003 AC1: two presentations of one token cannot both change the row,
    /// so the second is refused rather than lost.
    /// </summary>
    [Fact]
    public async Task AUTH_OIDC_003_AC1_TwoWritesOfOneRowCannotBothSucceedAsync()
    {
        SubjectId subject = await SignedInAsync();

        await RegisteredAsync(ClientId, OidcClientKind.Protocol);

        Guid grant = await GrantedAsync(subject);
        Guid issued = await TokenAsync(subject, grant, Noon, Noon + TimeSpan.FromDays(7));

        await using JanusDbContext first = database.Context();
        await using JanusDbContext second = database.Context();

        var one = new OidcTokenStore(first);
        var two = new OidcTokenStore(second);
        OidcTokenRecord held = (await FoundAsync(one, issued))!;
        OidcTokenRecord same = (await FoundAsync(two, issued))!;

        await one.SetStatusAsync(
            held,
            OpenIddictConstants.Statuses.Redeemed,
            TestContext.Current.CancellationToken);
        await one.UpdateAsync(held, TestContext.Current.CancellationToken);

        await two.SetStatusAsync(
            same,
            OpenIddictConstants.Statuses.Revoked,
            TestContext.Current.CancellationToken);

        _ = await Assert.ThrowsAsync<OpenIddictExceptions.ConcurrencyException>(async () =>
            await two.UpdateAsync(same, TestContext.Current.CancellationToken));
    }

    /// <inheritdoc/>
    public void Dispose() => _deployment.Dispose();

    private static async Task<ImmutableArray<string>> PermittedAsync(
        OidcApplicationStore applications,
        string clientId)
    {
        OidcClientRecord held = (await applications.FindByClientIdAsync(
            clientId,
            TestContext.Current.CancellationToken))!;

        return await applications.GetPermissionsAsync(held, TestContext.Current.CancellationToken);
    }

    private static async Task<OidcTokenRecord?> FoundAsync(OidcTokenStore tokens, Guid id) =>
        await tokens.FindByIdAsync(id.ToString(), TestContext.Current.CancellationToken);

    private SigningKeyStore Keys(JanusDbContext context) => new(context, _deployment.Keys);

    private async Task<Guid> GrantedAsync(SubjectId subject)
    {
        await using JanusDbContext writing = database.Context();

        var authorizations = new OidcAuthorizationStore(writing);
        OidcAuthorizationRecord grant = await authorizations.InstantiateAsync(
            TestContext.Current.CancellationToken);

        await authorizations.SetApplicationIdAsync(grant, ClientId, TestContext.Current.CancellationToken);
        await authorizations.SetSubjectAsync(
            grant,
            subject.ToString(),
            TestContext.Current.CancellationToken);
        await authorizations.SetStatusAsync(
            grant,
            OpenIddictConstants.Statuses.Valid,
            TestContext.Current.CancellationToken);
        await authorizations.SetTypeAsync(
            grant,
            OpenIddictConstants.AuthorizationTypes.AdHoc,
            TestContext.Current.CancellationToken);
        await authorizations.SetScopesAsync(
            grant,
            ["openid", "email"],
            TestContext.Current.CancellationToken);
        await authorizations.SetCreationDateAsync(grant, Noon, TestContext.Current.CancellationToken);
        await authorizations.CreateAsync(grant, TestContext.Current.CancellationToken);

        return grant.Id;
    }

    private async Task<Guid> TokenAsync(
        SubjectId subject,
        Guid grant,
        DateTimeOffset createdAt,
        DateTimeOffset expiresAt,
        string status = OpenIddictConstants.Statuses.Valid)
    {
        await using JanusDbContext writing = database.Context();

        var tokens = new OidcTokenStore(writing);
        OidcTokenRecord token = await tokens.InstantiateAsync(TestContext.Current.CancellationToken);

        await tokens.SetApplicationIdAsync(token, ClientId, TestContext.Current.CancellationToken);
        await tokens.SetAuthorizationIdAsync(
            token,
            grant.ToString(),
            TestContext.Current.CancellationToken);
        await tokens.SetSubjectAsync(token, subject.ToString(), TestContext.Current.CancellationToken);
        await tokens.SetStatusAsync(token, status, TestContext.Current.CancellationToken);
        await tokens.SetTypeAsync(
            token,
            OpenIddictConstants.TokenTypeHints.RefreshToken,
            TestContext.Current.CancellationToken);
        await tokens.SetCreationDateAsync(token, createdAt, TestContext.Current.CancellationToken);
        await tokens.SetExpirationDateAsync(token, expiresAt, TestContext.Current.CancellationToken);
        await tokens.CreateAsync(token, TestContext.Current.CancellationToken);

        return token.Id;
    }

    private async Task RegisteredAsync(string clientId, OidcClientKind kind)
    {
        var client = new OidcClient(
            clientId,
            "The " + clientId,
            kind,
            Destination,
            ["openid", "email", "offline_access"]);

        await using JanusDbContext writing = database.Context();

        await new OidcClientStore(writing).RecordAsync(
            client,
            OpaqueToken.Of(Secret).Fingerprint(),
            TestContext.Current.CancellationToken);
        await writing.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task<SubjectId> SignedInAsync()
    {
        SubjectId subject = await _deployment.AccountAsync(Noon);
        var record = Session.Begin(
            SessionId.New(TimeProvider.System),
            subject,
            new Assurance(AssuranceLevel.Aal2, PhishingResistant: true),
            new SessionOrigin("198.51.100.7", new DeviceDescription("Firefox", "Linux")) { Location = new SessionLocation("Cairo", "EG") },
            Noon,
            TimeSpan.FromDays(1),
            TimeSpan.FromDays(30),
            satisfiesEveryGate: true);

        await using JanusDbContext writing = database.Context();

        await new SessionStore(writing, _deployment.Keys, _deployment.Randomness).AddAsync(
            record,
            OpaqueToken.Draw(_deployment.Randomness).Fingerprint(),
            OpaqueToken.Draw(_deployment.Randomness).Fingerprint(),
            TestContext.Current.CancellationToken);
        await writing.SaveChangesAsync(TestContext.Current.CancellationToken);

        return subject;
    }
}
