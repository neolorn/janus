using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Janus.Authentication.Oidc;
using Janus.Core;
using Janus.Core.Configuration;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace Janus.Authentication.Tests.Oidc;

/// <summary>
/// The keys the provider signs with, as the credential source holds them: when the next
/// key is made and made current, how long a replaced key stays published and kept, what
/// the source disposes, and what two processes over the same keys do (AUTH-KEY-001,
/// OPS-SEC-002, CONV-CODE-007).
/// </summary>
[Trait("kind", "unit")]
public sealed class SigningKeysTests : IAsyncDisposable
{
    private static readonly DateTimeOffset Noon = new(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);

    private static readonly TimeSpan Cadence = TimeSpan.FromDays(90);

    private static readonly TimeSpan Lead = TimeSpan.FromMinutes(5);

    private static readonly TimeSpan Second = TimeSpan.FromSeconds(1);

    private readonly SigningKeyStoreInMemory _keys = new();
    private readonly ConfigurationInMemory _configuration = new();
    private readonly FixedClock _clock = new(Noon);
    private readonly SigningProcess _process;

    /// <summary>
    /// Starts the one process most cases need.
    /// </summary>
    public SigningKeysTests() => _process = new SigningProcess(_keys, _configuration, _clock);

    /// <inheritdoc/>
    public async ValueTask DisposeAsync() => await _process.DisposeAsync();

    /// <summary>
    /// AUTH-KEY-001 AC1: the first read once the cadence has passed since the current
    /// key began signing, the next key published for five minutes, makes the next key
    /// current, in the process already running and with nobody asked.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_KEY_001_AC1_TheFirstReadOnceTheCadenceHasPassedMakesTheNextKeyCurrentAsync()
    {
        string first = KeyId(await _process.SigningAsync(accessToken: false));

        _clock.Advance(Cadence - Lead);

        SigningKeySet published = await _process.ReadAsync();

        _clock.Advance(Lead);

        string second = KeyId(await _process.SigningAsync(accessToken: false));

        Assert.Equal(first, KeyId(published.Signing));
        Assert.NotNull(published.Keys.SingleOrDefault(held => held.Key.IsNext));
        Assert.NotEqual(first, second);
        Assert.Equal(published.Keys.Single(held => held.Key.IsNext).Key.KeyId, second);
        Assert.Equal(2, _keys.Count);
    }

    /// <summary>
    /// AUTH-KEY-001 AC1: before the cadence less five minutes has passed, a read changes
    /// nothing and does not read the stored keys, since whether a change is due is
    /// judged from the times the set carries.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_KEY_001_AC1_NoReadChangesAKeyBeforeItsTimeAsync()
    {
        string first = KeyId(await _process.SigningAsync(accessToken: false));
        int reads = _keys.Reads;

        _clock.Advance(Cadence - Lead - Second);

        string still = KeyId(await _process.SigningAsync(accessToken: false));

        Assert.Equal(first, still);
        Assert.Equal(1, _keys.Count);
        Assert.Equal(reads, _keys.Reads);
    }

    /// <summary>
    /// OPS-SEC-002 AC1: one running instance, never restarted, signs with a new key once
    /// the cadence has passed, and nobody asked for the key.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task OPS_SEC_002_AC1_TheRunningInstanceRotatesWithoutRestartOrAPersonAsync()
    {
        SigningCredentials first = await _process.SigningAsync(accessToken: true);

        await RotatedAsync(_process, _clock);

        SigningCredentials second = await _process.SigningAsync(accessToken: true);

        Assert.NotEqual(KeyId(first), KeyId(second));
        Assert.Equal(2, _keys.Count);
    }

    /// <summary>
    /// AUTH-KEY-001 AC2: the overlap is the longest access-token lifetime the replaced
    /// key signed under, never lowered by a shorter one signed after it, plus five
    /// minutes.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_KEY_001_AC2_TheOverlapIsTheLongestLifetimeTheKeySignedUnderAndFiveMinutesAsync()
    {
        _configuration.Set(Settings.OidcAccessTokenLifetime, TimeSpan.FromMinutes(30));

        string first = KeyId(await _process.SigningAsync(accessToken: true));

        _configuration.Set(Settings.OidcAccessTokenLifetime, TimeSpan.FromMinutes(10));

        _ = await _process.SigningAsync(accessToken: true);

        Assert.Equal(TimeSpan.FromMinutes(30), _keys.Held(first)!.LongestLifetime);

        await RotatedAsync(_process, _clock);
        _clock.Advance(TimeSpan.FromMinutes(35) - Second);

        Assert.Contains(first, Published(await _process.ReadAsync()));

        _clock.Advance(Second);

        Assert.DoesNotContain(first, Published(await _process.ReadAsync()));
    }

    /// <summary>
    /// AUTH-KEY-001 AC2: the end of the overlap is stored at the replacement, so a
    /// lifetime shortened after the rotation does not shorten it.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_KEY_001_AC2_NoChangeOfTheLifetimeAfterTheRotationShortensTheOverlapAsync()
    {
        _configuration.Set(Settings.OidcAccessTokenLifetime, TimeSpan.FromHours(1));

        string first = KeyId(await _process.SigningAsync(accessToken: true));

        await RotatedAsync(_process, _clock);

        _configuration.Set(Settings.OidcAccessTokenLifetime, TimeSpan.FromMinutes(5));
        _ = await _process.SigningAsync(accessToken: true);
        _clock.Advance(TimeSpan.FromMinutes(65) - Second);

        Assert.Contains(first, Published(await _process.ReadAsync()));
    }

    /// <summary>
    /// AUTH-KEY-001 AC2: the longer lifetime is stored with the current key, and
    /// committed, before the access token is signed under it; a token that is not an
    /// access token stores none.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_KEY_001_AC2_ALongerLifetimeIsCommittedBeforeTheAccessTokenIsSignedAsync()
    {
        string first = KeyId(await _process.SigningAsync(accessToken: false));

        _process.Work.Reset();
        _configuration.Set(Settings.OidcAccessTokenLifetime, TimeSpan.FromMinutes(20));

        _ = await _process.SigningAsync(accessToken: true);

        Assert.Equal(TimeSpan.FromMinutes(20), _keys.Held(first)!.LongestLifetime);
        Assert.Equal(1, _process.Work.Committed);

        _configuration.Set(Settings.OidcAccessTokenLifetime, TimeSpan.FromMinutes(40));

        _ = await _process.SigningAsync(accessToken: false);

        Assert.Equal(TimeSpan.FromMinutes(20), _keys.Held(first)!.LongestLifetime);
    }

    /// <summary>
    /// OPS-SEC-002 AC2, AUTH-KEY-001 AC2: what the previous key signed still verifies
    /// against the published set through the overlap after the next key took over.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task OPS_SEC_002_AC2_WhatThePreviousKeySignedVerifiesThroughTheOverlapAsync()
    {
        byte[] payload = RandomNumberGenerator.GetBytes(64);
        SigningCredentials previous = await _process.SigningAsync(accessToken: true);
        byte[] signature = ((ECDsaSecurityKey)previous.Key).ECDsa.SignData(payload, HashAlgorithmName.SHA256);

        await RotatedAsync(_process, _clock);
        _clock.Advance(TimeSpan.FromMinutes(14));

        HeldSigningKey published = (await _process.ReadAsync())
            .Published(_clock.GetUtcNow())
            .Single(held => held.Key.KeyId == KeyId(previous));

        Assert.True(Verifies(published.PublicKey, payload, signature));
    }

    /// <summary>
    /// AUTH-KEY-001 AC3: once the overlap has passed, the previous key is gone from what
    /// the set publishes, though the set still keeps its public key.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_KEY_001_AC3_ThePreviousKeyLeavesThePublishedSetAfterTheOverlapAsync()
    {
        string first = KeyId(await _process.SigningAsync(accessToken: true));

        await RotatedAsync(_process, _clock);
        _clock.Advance(TimeSpan.FromMinutes(15));

        SigningKeySet set = await _process.ReadAsync();

        Assert.DoesNotContain(first, Published(set));
        Assert.Contains(set.Keys, held => held.Key.KeyId == first);
    }

    /// <summary>
    /// AUTH-KEY-001 AC4: every published key, the next key among them, carries the
    /// configured algorithm, so a relying party that accepts that one accepts all of
    /// them.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_KEY_001_AC4_EveryPublishedKeyCarriesTheConfiguredAlgorithmAsync()
    {
        _ = await _process.ReadAsync();
        _clock.Advance(Cadence - Lead);

        IReadOnlyList<HeldSigningKey> published = (await _process.ReadAsync()).Published(_clock.GetUtcNow());

        Assert.Equal(2, published.Count);
        Assert.All(published, held => Assert.Equal("ES256", held.Key.Algorithm));
        Assert.All(published, held => Assert.Equal("ES256", held.PublicKey.Alg));
        Assert.Equal("ES256", (await _process.SigningAsync(accessToken: true)).Algorithm);
    }

    /// <summary>
    /// AUTH-KEY-001 AC4: what the store is handed is the private material, and what the
    /// set publishes is the public half alone.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_KEY_001_AC4_ThePublishedSetCarriesThePublicHalfAloneAsync()
    {
        string keyId = KeyId(await _process.SigningAsync(accessToken: false));

        byte[] privateKey = (await _keys.PrivateKeyAsync(keyId, TestContext.Current.CancellationToken))!;
        HeldSigningKey published = (await _process.ReadAsync()).Published(_clock.GetUtcNow()).Single();

        using var held = ECDsa.Create();

        held.ImportPkcs8PrivateKey(privateKey, out _);

        ECParameters expected = held.ExportParameters(includePrivateParameters: false);

        Assert.Null(published.PublicKey.D);
        Assert.Equal(Base64UrlEncoder.Encode(expected.Q.X), published.PublicKey.X);
        Assert.Equal(Base64UrlEncoder.Encode(expected.Q.Y), published.PublicKey.Y);
    }

    /// <summary>
    /// AUTH-KEY-001 AC5: a replaced key signs no token after the rotation, an access
    /// token or any other.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_KEY_001_AC5_AReplacedKeySignsNothingAfterTheRotationAsync()
    {
        string first = KeyId(await _process.SigningAsync(accessToken: true));

        await RotatedAsync(_process, _clock);

        Assert.NotEqual(first, KeyId(await _process.SigningAsync(accessToken: true)));
        Assert.NotEqual(first, KeyId(await _process.SigningAsync(accessToken: false)));
    }

    /// <summary>
    /// AUTH-KEY-001 AC5, CONV-CODE-007 AC4: the first read after the overlap ends
    /// retires the replaced key: its private key object is disposed and its private key
    /// removed from the database, and the set holds it by its public key alone.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_KEY_001_AC5_TheFirstReadAfterTheOverlapRetiresTheKeyAsync()
    {
        _ = await _process.ReadAsync();

        SigningCredentials started = _process.Source.Started;

        _ = await _process.SigningAsync(accessToken: true);
        await RotatedAsync(_process, _clock);
        _clock.Advance(TimeSpan.FromMinutes(15) - Second);

        SigningKeySet before = await _process.ReadAsync();

        Assert.True(_keys.HoldsPrivateKey(KeyId(started)));
        Assert.NotEmpty(Signed(started));
        Assert.Same(started, before.Keys.Single(held => held.Key.KeyId == KeyId(started)).Credentials);

        _clock.Advance(Second);

        SigningKeySet after = await _process.ReadAsync();

        Assert.False(_keys.HoldsPrivateKey(KeyId(started)));
        Assert.Throws<ObjectDisposedException>(() => Signed(started));
        Assert.Null(after.Keys.Single(held => held.Key.KeyId == KeyId(started)).Credentials);
    }

    /// <summary>
    /// CONV-CODE-007 AC4: a replaced key holds its public key apart from its private key
    /// object, so what it signed still verifies against the set once the object is
    /// disposed.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task CONV_CODE_007_AC4_AReplacedKeyHoldsItsPublicKeyApartFromItsPrivateKeyObjectAsync()
    {
        byte[] payload = RandomNumberGenerator.GetBytes(64);
        SigningCredentials previous = await _process.SigningAsync(accessToken: true);
        byte[] signature = ((ECDsaSecurityKey)previous.Key).ECDsa.SignData(payload, HashAlgorithmName.SHA256);

        await RotatedAsync(_process, _clock);
        _clock.Advance(TimeSpan.FromMinutes(15));

        HeldSigningKey kept = (await _process.ReadAsync()).Keys.Single(held => held.Key.KeyId == KeyId(previous));

        Assert.Throws<ObjectDisposedException>(() => Signed(previous));
        Assert.True(Verifies(kept.PublicKey, payload, signature));
    }

    /// <summary>
    /// CONV-CODE-007 AC4: a set that replaces another carries the object already made
    /// for each key it keeps, so the current key signs with the one object from the
    /// start until it is replaced, and after, until its retirement disposes it.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task CONV_CODE_007_AC4_ASetThatReplacesAnotherCarriesTheObjectsItKeepsAsync()
    {
        _ = await _process.ReadAsync();

        SigningCredentials started = _process.Source.Started;

        _clock.Advance(Cadence - Lead);

        SigningKeySet published = await _process.ReadAsync();

        Assert.Same(started, published.Signing);

        _clock.Advance(Lead);

        SigningKeySet rotated = await _process.ReadAsync();

        Assert.Same(started, rotated.Keys.Single(held => held.Key.KeyId == KeyId(started)).Credentials);
        Assert.Same(
            published.Keys.Single(held => held.Key.IsNext).Credentials,
            rotated.Signing);
    }

    /// <summary>
    /// CONV-CODE-007 AC4: the library's copy of the bytes each key was made from, the
    /// one handed to the store and each one read back from it, is zero when the making
    /// returns.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task CONV_CODE_007_AC4_TheBytesAKeyIsMadeFromAreZeroWhenTheMakingReturnsAsync()
    {
        _ = await _process.ReadAsync();
        await RotatedAsync(_process, _clock);

        await using var restarted = new SigningProcess(_keys, _configuration, _clock);

        _ = await restarted.ReadAsync();

        Assert.Equal(2, _keys.Handed.Count);
        Assert.Equal(3, _keys.Lent.Count);
        Assert.All(_keys.Handed, bytes => Assert.All(bytes, octet => Assert.Equal(0, octet)));
        Assert.All(_keys.Lent, bytes => Assert.All(bytes, octet => Assert.Equal(0, octet)));
    }

    /// <summary>
    /// AUTH-KEY-001 AC6: a replaced key's public key stays in the set, unpublished,
    /// until the longest a session can last has passed since its replacement, and the
    /// first read after that removes it from the set and the database.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_KEY_001_AC6_AReplacedKeyIsKeptUntilTheLongestASessionCanLastAsync()
    {
        string first = KeyId(await _process.SigningAsync(accessToken: true));

        await RotatedAsync(_process, _clock);

        TimeSpan keeping = Settings.SessionDefaultAbsolute.Ceiling!.Value;

        _clock.Advance(keeping - Second);

        SigningKeySet kept = await _process.ReadAsync();

        Assert.Contains(kept.Keys, held => held.Key.KeyId == first);
        Assert.DoesNotContain(first, Published(kept));
        Assert.NotNull(_keys.Held(first));

        _clock.Advance(Second);

        SigningKeySet removed = await _process.ReadAsync();

        Assert.DoesNotContain(removed.Keys, held => held.Key.KeyId == first);
        Assert.Null(_keys.Held(first));
    }

    /// <summary>
    /// AUTH-KEY-001 AC7: where the database holds no signing key, the first read makes
    /// one current at once, and that is the credential the provider's options are built
    /// with.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_KEY_001_AC7_WhereTheDatabaseHoldsNoKeyTheFirstReadMakesOneCurrentAsync()
    {
        SigningKeySet set = await _process.ReadAsync();

        SigningKey current = Assert.Single(await _keys.HeldAsync(TestContext.Current.CancellationToken));

        Assert.True(current.IsCurrent);
        Assert.Equal(Noon, current.SigningFrom);
        Assert.Equal(current.KeyId, KeyId(set.Signing));
        Assert.Same(set.Signing, _process.Source.Started);
        Assert.Equal(1, _process.Work.Committed);
    }

    /// <summary>
    /// AUTH-KEY-001 AC7: of two processes that find the same change due, one makes it,
    /// and the other, finding it made, holds the set the stored keys give.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_KEY_001_AC7_OfTwoProcessesFindingTheSameChangeDueOneMakesItAsync()
    {
        await using var other = new SigningProcess(_keys, _configuration, _clock);

        _ = await _process.ReadAsync();
        _ = await other.ReadAsync();

        _clock.Advance(Cadence - Lead);

        SigningKeySet made = await _process.ReadAsync();
        SigningKeySet read = await other.ReadAsync();

        Assert.Equal(2, _keys.Count);
        Assert.Equal(Published(made), Published(read));
        Assert.Equal(KeyId(made.Signing), KeyId(read.Signing));
    }

    /// <summary>
    /// CONV-DESIGN-003 AC5, AUTH-KEY-001 AC7: a change another process made first answers
    /// success with nothing of this one committed, its transaction rolled back and left
    /// closed, and the stored key is the one that process made.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task CONV_DESIGN_003_AC5_AChangeAnotherProcessMadeFirstIsRolledBackAsync()
    {
        _ = await _process.ReadAsync();

        await using var work = new UnitOfWorkInMemory();
        var losing = new SigningKeys(_keys, _configuration, work, _clock);

        _keys.ReadsBeforeTheFirstKey = true;

        Result changed = await losing.ChangeAsync(TestContext.Current.CancellationToken);

        Assert.True(changed.Match(() => true, _ => false));
        Assert.False(work.Open);
        Assert.Equal((0, 1), (work.Committed, work.RolledBack));
        Assert.Equal(1, _keys.Count);
    }

    /// <summary>
    /// CONV-DESIGN-003 AC10, AUTH-KEY-001: a change asked for where none is due answers
    /// success having written nothing, so its unit of work is rolled back.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task CONV_DESIGN_003_AC10_AChangeWithNothingDueIsRolledBackAsync()
    {
        _ = await _process.ReadAsync();

        await using var work = new UnitOfWorkInMemory();
        var keys = new SigningKeys(_keys, _configuration, work, _clock);

        Result changed = await keys.ChangeAsync(TestContext.Current.CancellationToken);

        Assert.True(changed.Match(() => true, _ => false));
        Assert.False(work.Open);
        Assert.Equal((0, 1), (work.Committed, work.RolledBack));
        Assert.Equal(1, _keys.Count);
    }

    /// <summary>
    /// CONV-DESIGN-003 AC10, AUTH-KEY-001 AC7: a longer lifetime stored against a key that
    /// is no longer current writes nothing, so its unit of work is rolled back; against
    /// the current key it commits.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task CONV_DESIGN_003_AC10_ALifetimeAgainstAKeyNoLongerCurrentIsRolledBackAsync()
    {
        SigningKey first = (await _process.ReadAsync()).Keys.Single(held => held.Key.IsCurrent).Key;

        await using var work = new UnitOfWorkInMemory();
        var keys = new SigningKeys(_keys, _configuration, work, _clock);

        Result kept = await keys.LengthenAsync(first, TimeSpan.FromHours(2), TestContext.Current.CancellationToken);

        Assert.True(kept.Match(() => true, _ => false));
        Assert.Equal((1, 0), (work.OutermostCommitted, work.RolledBack));

        _clock.Advance(Cadence - Lead);
        _ = await _process.ReadAsync();
        _clock.Advance(Lead);
        _ = await _process.SigningAsync(accessToken: false);
        work.Reset();

        Result late = await keys.LengthenAsync(first, TimeSpan.FromHours(3), TestContext.Current.CancellationToken);

        Assert.True(late.Match(() => true, _ => false));
        Assert.False(work.Open);
        Assert.Equal((0, 1), (work.Committed, work.RolledBack));
    }

    /// <summary>
    /// AUTH-KEY-001 AC7: a longer lifetime stored against a key another process has just
    /// replaced is refused, and the token is signed by the current key, which carries
    /// the longer lifetime in turn. The other process's clock runs a little behind, so
    /// the times its set carries do not yet show the replacement.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_KEY_001_AC7_ALongerLifetimeAgainstAReplacedKeyIsRefusedAndTheCurrentKeySignsAsync()
    {
        var behind = new FixedClock(Noon - TimeSpan.FromMinutes(1));

        await using var other = new SigningProcess(_keys, _configuration, behind);

        string first = KeyId(await _process.SigningAsync(accessToken: false));

        _clock.Advance(Cadence - Lead);
        behind.Advance(Cadence - Lead + TimeSpan.FromMinutes(1));

        string next = (await _process.ReadAsync()).Keys.Single(held => held.Key.IsNext).Key.KeyId;

        Assert.Contains(next, Published(await other.ReadAsync()));

        _clock.Advance(Lead);
        behind.Advance(Lead - TimeSpan.FromMinutes(2));

        Assert.Equal(next, KeyId(await _process.SigningAsync(accessToken: false)));

        _configuration.Set(Settings.OidcAccessTokenLifetime, TimeSpan.FromMinutes(20));

        string signed = KeyId(await other.SigningAsync(accessToken: true));

        Assert.Equal(next, signed);
        Assert.Equal(TimeSpan.Zero, _keys.Held(first)!.LongestLifetime);
        Assert.Equal(TimeSpan.FromMinutes(20), _keys.Held(next)!.LongestLifetime);
    }

    /// <summary>
    /// AUTH-KEY-001 AC8: a key made by a read after the cadence has passed signs nothing
    /// until it has been published for five minutes.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_KEY_001_AC8_AKeyMadeAfterTheCadenceSignsOnceItHasBeenPublishedFiveMinutesAsync()
    {
        string first = KeyId(await _process.SigningAsync(accessToken: false));

        _clock.Advance(Cadence + TimeSpan.FromHours(1));

        Assert.Equal(first, KeyId(await _process.SigningAsync(accessToken: false)));

        _clock.Advance(Lead - Second);

        Assert.Equal(first, KeyId(await _process.SigningAsync(accessToken: false)));

        _clock.Advance(Second);

        Assert.NotEqual(first, KeyId(await _process.SigningAsync(accessToken: false)));
    }

    /// <summary>
    /// AUTH-KEY-001 AC8: over many rotations, read at uneven intervals, every key but the
    /// first is published for at least five minutes before it signs its first token.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_KEY_001_AC8_EveryKeyButTheFirstIsPublishedFiveMinutesBeforeItSignsAsync()
    {
        _configuration.Set(Settings.TokenSigningRotation, TimeSpan.FromHours(1));

        var publishedFrom = new Dictionary<string, DateTimeOffset>(StringComparer.Ordinal);
        var signingFrom = new Dictionary<string, DateTimeOffset>(StringComparer.Ordinal);
        int[] steps = [1, 7, 13, 2, 29, 3, 11];

        for (int read = 0; read < 400; read++)
        {
            DateTimeOffset now = _clock.GetUtcNow();
            SigningKeySet set = await _process.ReadAsync();

            foreach (string keyId in Published(set))
            {
                _ = publishedFrom.TryAdd(keyId, now);
            }

            _ = signingFrom.TryAdd(KeyId(await _process.SigningAsync(accessToken: true)), now);

            _clock.Advance(TimeSpan.FromMinutes(steps[read % steps.Length]));
        }

        Assert.True(signingFrom.Count > 10);
        Assert.All(
            signingFrom.Skip(1),
            signing => Assert.True(signing.Value - publishedFrom[signing.Key] >= Lead));
    }

    private static string KeyId(SigningCredentials credentials) => credentials.Key.KeyId;

    private List<string> Published(SigningKeySet set) =>
        [.. set.Published(_clock.GetUtcNow()).Select(held => held.Key.KeyId)];

    private static byte[] Signed(SigningCredentials credentials) =>
        ((ECDsaSecurityKey)credentials.Key).ECDsa.SignData([1, 2, 3], HashAlgorithmName.SHA256);

    private static bool Verifies(JsonWebKey published, byte[] payload, byte[] signature)
    {
        using var verifier = ECDsa.Create(new ECParameters
        {
            Curve = ECCurve.NamedCurves.nistP256,
            Q = new ECPoint
            {
                X = Base64UrlEncoder.DecodeBytes(published.X),
                Y = Base64UrlEncoder.DecodeBytes(published.Y),
            },
        });

        return verifier.VerifyData(payload, signature, HashAlgorithmName.SHA256);
    }

    // The next key made at the cadence less five minutes, and made current five minutes
    // later, each by the first read that finds it due.
    private static async Task RotatedAsync(SigningProcess process, FixedClock clock)
    {
        clock.Advance(Cadence - Lead);
        _ = await process.ReadAsync();
        clock.Advance(Lead);
        _ = await process.ReadAsync();
    }
}
