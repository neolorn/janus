using System;
using System.Collections.Generic;
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
using Xunit;

namespace Janus.Storage.Tests.Authentication;

/// <summary>
/// What the provider keeps: the registry a client is read from, the code and the
/// refresh token held as what they hash to, the signing key whose private half is
/// wrapped, and the sweep that takes what has expired (AUTH-OIDC-001, AUTH-OIDC-003,
/// AUTH-KEY-001, AUTH-KEY-002, AUTH-KEY-003).
/// </summary>
[Trait("kind", "integration")]
public sealed class OidcStoreTests(DatabaseFixture database)
    : IClassFixture<DatabaseFixture>, IDisposable
{
    private const string ClientId = "mail-server";
    private const string Destination = "https://mail.example.test/signin/callback";
    private const string Secret = "a-secret-the-deployment-set";

    private static readonly DateTimeOffset Noon = new(2026, 9, 19, 12, 0, 0, TimeSpan.Zero);

    private readonly Deployment _deployment = new(database);

    /// <summary>
    /// AUTH-OIDC-001 AC2: the registry authenticates a client by what its secret
    /// hashes to, and the column holds nothing the secret could be read out of.
    /// </summary>
    [Fact]
    public async Task AUTH_OIDC_001_AC2_TheRegistryHoldsWhatTheSecretHashesToAsync()
    {
        await RegisteredAsync();

        await using NpgsqlConnection connection = await database.OpenAsync();

        byte[] stored = await connection.QuerySingleAsync<byte[]>(
            "SELECT secret FROM janus.oidc_clients WHERE client_id = @clientId",
            new { clientId = ClientId });

        Assert.Equal(OpaqueToken.Of(Secret).Fingerprint(), stored);

        await using JanusDbContext reading = database.Context();

        Assert.True(await new OidcClientStore(reading).AuthenticatesAsync(
            ClientId,
            OpaqueToken.Of(Secret).Fingerprint(),
            TestContext.Current.CancellationToken));
        Assert.False(await new OidcClientStore(reading).AuthenticatesAsync(
            ClientId,
            OpaqueToken.Of("not-the-secret").Fingerprint(),
            TestContext.Current.CancellationToken));
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
    /// AUTH-KEY-003 AC1: a code that has expired is taken by the sweep, which is one
    /// call and no person's task; one that has not is left where it is.
    /// </summary>
    [Fact]
    public async Task AUTH_KEY_003_AC1_TheSweepTakesTheCodesThatHaveExpiredAsync()
    {
        (SubjectId subject, SessionId session) = await SignedInAsync();

        OidcClient client = await RegisteredAsync();
        AuthorizationCode spent = Code(client, subject, session, TimeSpan.FromSeconds(60));
        AuthorizationCode fresh = Code(client, subject, session, TimeSpan.FromHours(1));

        await using (JanusDbContext writing = database.Context())
        {
            await new AuthorizationCodeStore(writing)
                .AddAsync(spent, TestContext.Current.CancellationToken);
            await new AuthorizationCodeStore(writing)
                .AddAsync(fresh, TestContext.Current.CancellationToken);
            await writing.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using JanusDbContext sweeping = database.Context();

        Assert.Equal(
            1,
            await new AuthorizationCodeStore(sweeping).SweepAsync(
                Noon + TimeSpan.FromMinutes(5),
                TestContext.Current.CancellationToken));

        await using JanusDbContext reading = database.Context();

        Assert.Null(await new AuthorizationCodeStore(reading)
            .FindAsync(spent.Fingerprint, TestContext.Current.CancellationToken));
        Assert.NotNull(await new AuthorizationCodeStore(reading)
            .FindAsync(fresh.Fingerprint, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// AUTH-KEY-003 AC1: a refresh token that has passed the expiry the session gave
    /// it is taken by the same sweep, consumed or not.
    /// </summary>
    [Fact]
    public async Task AUTH_KEY_003_AC1_TheSweepTakesTheRefreshTokensThatHaveExpiredAsync()
    {
        (SubjectId subject, SessionId session) = await SignedInAsync();

        _ = await RegisteredAsync();

        RefreshToken spent = Token(subject, session, Noon + TimeSpan.FromMinutes(1));
        RefreshToken live = Token(subject, session, Noon + TimeSpan.FromDays(7));

        await using (JanusDbContext writing = database.Context())
        {
            await new RefreshTokenStore(writing).AddAsync(spent, TestContext.Current.CancellationToken);
            await new RefreshTokenStore(writing).AddAsync(live, TestContext.Current.CancellationToken);
            await writing.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using JanusDbContext sweeping = database.Context();

        Assert.Equal(
            1,
            await new RefreshTokenStore(sweeping).SweepAsync(
                Noon + TimeSpan.FromMinutes(5),
                TestContext.Current.CancellationToken));

        await using JanusDbContext reading = database.Context();

        Assert.Null(await new RefreshTokenStore(reading)
            .FindAsync(spent.Fingerprint, TestContext.Current.CancellationToken));
        Assert.NotNull(await new RefreshTokenStore(reading)
            .FindAsync(live.Fingerprint, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// AUTH-OIDC-003 AC1: a family is removed whole, which is what a reuse costs every
    /// token standing beside the one presented.
    /// </summary>
    [Fact]
    public async Task AUTH_OIDC_003_AC1_AFamilyIsRemovedWholeAsync()
    {
        (SubjectId subject, SessionId session) = await SignedInAsync();

        _ = await RegisteredAsync();

        var family = RefreshFamilyId.New(TimeProvider.System);
        RefreshToken first = Token(subject, session, Noon + TimeSpan.FromDays(7), family);
        RefreshToken second = Token(subject, session, Noon + TimeSpan.FromDays(7), family);
        RefreshToken other = Token(subject, session, Noon + TimeSpan.FromDays(7));

        await using (JanusDbContext writing = database.Context())
        {
            foreach (RefreshToken token in new[] { first, second, other })
            {
                await new RefreshTokenStore(writing).AddAsync(token, TestContext.Current.CancellationToken);
            }

            await writing.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using JanusDbContext removing = database.Context();

        await new RefreshTokenStore(removing)
            .RemoveFamilyAsync(family, TestContext.Current.CancellationToken);

        await using JanusDbContext reading = database.Context();

        Assert.Empty(await new RefreshTokenStore(reading)
            .OfAsync(family, TestContext.Current.CancellationToken));
        Assert.NotNull(await new RefreshTokenStore(reading)
            .FindAsync(other.Fingerprint, TestContext.Current.CancellationToken));
    }

    /// <inheritdoc/>
    public void Dispose() => _deployment.Dispose();

    private static AuthorizationCode Code(
        OidcClient client,
        SubjectId subject,
        SessionId session,
        TimeSpan lifetime) =>
        AuthorizationCode.Issue(
            RandomNumberGenerator.GetBytes(32),
            client,
            subject,
            session,
            Destination,
            "a-challenge-of-the-verifier",
            "S256",
            "openid email",
            nonce: null,
            Noon,
            lifetime);

    private static RefreshToken Token(
        SubjectId subject,
        SessionId session,
        DateTimeOffset expiresAt,
        RefreshFamilyId? family = null) =>
        RefreshToken.Issue(
            RandomNumberGenerator.GetBytes(32),
            family ?? RefreshFamilyId.New(TimeProvider.System),
            ClientId,
            subject,
            session,
            "openid email offline_access",
            Noon,
            expiresAt);

    private SigningKeyStore Keys(JanusDbContext context) => new(context, _deployment.Keys);

    private async Task<OidcClient> RegisteredAsync()
    {
        var client = new OidcClient(
            ClientId,
            "The mail server",
            OidcClientKind.Protocol,
            Destination,
            ["openid", "email", "offline_access"]);

        await using JanusDbContext writing = database.Context();

        await new OidcClientStore(writing).RecordAsync(
            client,
            OpaqueToken.Of(Secret).Fingerprint(),
            TestContext.Current.CancellationToken);
        await writing.SaveChangesAsync(TestContext.Current.CancellationToken);

        return client;
    }

    private async Task<(SubjectId Subject, SessionId Session)> SignedInAsync()
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

        return (subject, record.Id);
    }
}
