using System;
using System.Collections.Immutable;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
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
/// AUTH-KEY-003, OPS-SEC-001, OPS-SEC-002).
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
    /// OPS-SEC-001, AUTH-OIDC-001 AC2: the registry holds a client's secret wrapped under
    /// the deployment's data key, neither in the clear nor as anything a search over
    /// the table could reach, and what the protocol server is handed is that same
    /// wrapped value, which authenticates nothing.
    /// </summary>
    [Fact]
    public async Task OPS_SEC_001_NoClientSecretIsHeldInTheClearAsync()
    {
        await RegisteredAsync(ClientId, OidcClientKind.Protocol);

        await using NpgsqlConnection connection = await database.OpenAsync();

        byte[] stored = await connection.QuerySingleAsync<byte[]>(
            "SELECT secret FROM identity.oidc_clients WHERE client_id = @clientId",
            new { clientId = ClientId });

        await using StoreContext reading = database.Context();

        byte[] deploymentKey = await _deployment.DataKey(reading).UnwrappedAsync(TestContext.Current.CancellationToken);

        Assert.NotEqual(Encoding.UTF8.GetBytes(Secret), stored);
        Assert.NotEqual(SHA256.HashData(Encoding.UTF8.GetBytes(Secret)), stored);
        Assert.False(stored.AsSpan().IndexOf(Encoding.UTF8.GetBytes(Secret)) >= 0);
        Assert.Equal(Encoding.UTF8.GetBytes(Secret), PersonalFieldCipher.Unwrap(stored, deploymentKey));

        var applications = new OidcApplicationStore(reading);
        OidcClientRecord? held = await applications.FindByClientIdAsync(
            ClientId,
            TestContext.Current.CancellationToken);

        Assert.NotNull(held);
        Assert.Equal(
            Convert.ToBase64String(stored),
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

        await using StoreContext reading = database.Context();

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
    /// OPS-SEC-002 AC2, X3: a replacement stands only where the secret is still the one
    /// read, keeps the one it replaced wrapped until the overlap ends, and a change to
    /// the client leaves both alone; the table holds no replaced secret without the
    /// instant it ends.
    /// </summary>
    [Fact]
    public async Task OPS_SEC_002_AReplacementStandsOnlyWhereTheSecretIsTheOneReadAsync()
    {
        const string rotated = "rotated-client";

        byte[] replacement = Encoding.UTF8.GetBytes("the-secret-that-replaced-it");
        DateTimeOffset until = Noon + TimeSpan.FromMinutes(15);

        await RegisteredAsync(rotated, OidcClientKind.Protocol);

        bool replaced;
        bool stale;

        await using (StoreContext writing = database.Context())
        {
            var clients = new OidcClientStore(writing, _deployment.DataKey(writing));

            replaced = await clients.ReplaceSecretAsync(
                rotated, Noon, replacement, Noon + TimeSpan.FromDays(90), until, TestContext.Current.CancellationToken);
            stale = await clients.ReplaceSecretAsync(
                rotated, Noon, Encoding.UTF8.GetBytes("a-later-secret"), Noon + TimeSpan.FromDays(91), until, TestContext.Current.CancellationToken);
        }

        await RegisteredAsync(rotated, OidcClientKind.BrowserApplication);

        await using StoreContext reading = database.Context();

        RegisteredSecret held = (await new OidcClientStore(reading, _deployment.DataKey(reading))
            .SecretAsync(rotated, TestContext.Current.CancellationToken))!;

        Assert.True(replaced);
        Assert.False(stale);
        Assert.Equal(replacement, held.Current);
        Assert.Equal(Noon + TimeSpan.FromDays(90), held.IssuedAt);
        Assert.Equal(Encoding.UTF8.GetBytes(Secret), held.Previous);
        Assert.Equal(until, held.PreviousUntil);

        await using NpgsqlConnection connection = await database.OpenAsync();

        PostgresException refused = await Assert.ThrowsAsync<PostgresException>(async () =>
            await connection.ExecuteAsync(
                "UPDATE identity.oidc_clients SET previous_secret_until = NULL WHERE client_id = @clientId",
                new { clientId = rotated }));

        Assert.Equal(PostgresErrorCodes.CheckViolation, refused.SqlState);
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

        await using StoreContext reading = database.Context();

        var applications = new OidcApplicationStore(reading);

        Assert.Contains(
            OpenIddictConstants.Permissions.GrantTypes.RefreshToken,
            await PermittedAsync(applications, ClientId));
        Assert.DoesNotContain(
            OpenIddictConstants.Permissions.GrantTypes.RefreshToken,
            await PermittedAsync(applications, Browser));
    }

    /// <summary>
    /// AUTH-KEY-002, PRIV-RIGHT-005a AC16: the private half of a signing key is at rest
    /// under the deployment's data key and not under the key-encryption key itself, and
    /// is readable again only through the store.
    /// </summary>
    [Fact]
    public async Task AUTH_KEY_002_ThePrivateHalfIsWrappedUnderTheDeploymentDataKeyAsync()
    {
        using var created = ECDsa.Create(ECCurve.NamedCurves.nistP256);

        byte[] privateKey = created.ExportPkcs8PrivateKey();
        var key = SigningKey.Create("the-key", "ES256", created.ExportSubjectPublicKeyInfo(), Noon);

        await using (StoreContext writing = database.Context())
        {
            await Keys(writing).AddAsync(key, privateKey, TestContext.Current.CancellationToken);
            await writing.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using NpgsqlConnection connection = await database.OpenAsync();

        byte[] held = await connection.QuerySingleAsync<byte[]>(
            "SELECT private_key FROM identity.signing_keys WHERE key_id = @keyId",
            new { keyId = key.KeyId });

        await using StoreContext reading = database.Context();

        byte[] deploymentKey = await _deployment.DataKey(reading).UnwrappedAsync(TestContext.Current.CancellationToken);

        Assert.NotEqual(privateKey, held);
        Assert.Equal(privateKey, PersonalFieldCipher.Unwrap(held, deploymentKey));
        Assert.Throws<CryptographicException>(() => PersonalFieldCipher.Unwrap(held, _deployment.Keys.Current.Span));

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

        await using (StoreContext sweeping = database.Context())
        {
            Assert.True(
                await new OidcTokenStore(sweeping, TimeProvider.System).PruneAsync(
                    Noon + TimeSpan.FromMinutes(5),
                    TestContext.Current.CancellationToken) >= 2);
        }

        await using StoreContext reading = database.Context();

        var tokens = new OidcTokenStore(reading, TimeProvider.System);

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

        await using (StoreContext revoking = database.Context())
        {
            Assert.Equal(
                2,
                await new OidcTokenStore(revoking, TimeProvider.System).RevokeByAuthorizationIdAsync(
                    reused.ToString(),
                    TestContext.Current.CancellationToken));
        }

        await using StoreContext reading = database.Context();

        var tokens = new OidcTokenStore(reading, TimeProvider.System);

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

        await using StoreContext first = database.Context();
        await using StoreContext second = database.Context();

        var one = new OidcTokenStore(first, TimeProvider.System);
        var two = new OidcTokenStore(second, TimeProvider.System);
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

    /// <summary>
    /// AUTH-OIDC-006 AC2: a pushed request is kept before anyone is known, so its row
    /// names nobody, and a subject that names no account of this deployment is still
    /// refused where it arrives.
    /// </summary>
    [Fact]
    public async Task AUTH_OIDC_006_AC2_APushedRequestIsKeptNamingNobodyAsync()
    {
        await RegisteredAsync(Browser, OidcClientKind.BrowserApplication);

        Guid pushed;

        await using (StoreContext writing = database.Context())
        {
            var tokens = new OidcTokenStore(writing, TimeProvider.System);
            OidcTokenRecord token = await tokens.InstantiateAsync(TestContext.Current.CancellationToken);

            await tokens.SetApplicationIdAsync(token, Browser, TestContext.Current.CancellationToken);
            await tokens.SetSubjectAsync(token, subject: null, TestContext.Current.CancellationToken);
            await tokens.SetStatusAsync(
                token,
                OpenIddictConstants.Statuses.Valid,
                TestContext.Current.CancellationToken);
            await tokens.SetTypeAsync(
                token,
                OpenIddictConstants.TokenTypeIdentifiers.Private.RequestToken,
                TestContext.Current.CancellationToken);
            await tokens.SetCreationDateAsync(token, Noon, TestContext.Current.CancellationToken);
            await tokens.SetExpirationDateAsync(
                token,
                Noon + TimeSpan.FromSeconds(60),
                TestContext.Current.CancellationToken);
            await tokens.CreateAsync(token, TestContext.Current.CancellationToken);

            _ = await Assert.ThrowsAsync<ArgumentException>(async () =>
                await tokens.SetSubjectAsync(
                    token,
                    "someone-else",
                    TestContext.Current.CancellationToken));

            pushed = token.Id;
        }

        await using StoreContext reading = database.Context();

        var read = new OidcTokenStore(reading, TimeProvider.System);
        OidcTokenRecord held = (await FoundAsync(read, pushed))!;

        Assert.Null(await read.GetSubjectAsync(held, TestContext.Current.CancellationToken));
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

    private SigningKeyStore Keys(StoreContext context) => new(context, _deployment.DataKey(context));

    private async Task<Guid> GrantedAsync(SubjectId subject)
    {
        await using StoreContext writing = database.Context();

        var authorizations = new OidcAuthorizationStore(writing, TimeProvider.System);
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
        await using StoreContext writing = database.Context();

        var tokens = new OidcTokenStore(writing, TimeProvider.System);
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

    // Registers the client with the secret drawn at noon, or carries the change to the
    // one the registry holds, as the client registry does.
    private async Task RegisteredAsync(string clientId, OidcClientKind kind)
    {
        var client = new OidcClient(
            clientId,
            "The " + clientId,
            kind,
            Destination,
            ["openid", "email", "offline_access"]);

        await using StoreContext writing = database.Context();

        var clients = new OidcClientStore(writing, _deployment.DataKey(writing));

        if (await clients.FindAsync(clientId, TestContext.Current.CancellationToken) is null)
        {
            await clients.AddAsync(client, Encoding.UTF8.GetBytes(Secret), Noon, TestContext.Current.CancellationToken);
        }
        else
        {
            await clients.RecordAsync(client, TestContext.Current.CancellationToken);
        }

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
            breakGlassReason: "The operator cannot be reached.");

        await using StoreContext writing = database.Context();

        await new SessionStore(writing, _deployment.Keys, _deployment.Randomness).AddAsync(
            record,
            OpaqueToken.Draw(_deployment.Randomness).Fingerprint(),
            OpaqueToken.Draw(_deployment.Randomness).Fingerprint(),
            TestContext.Current.CancellationToken);
        await writing.SaveChangesAsync(TestContext.Current.CancellationToken);

        return subject;
    }
}
