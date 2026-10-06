using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Janus.Authentication.Oidc;
using Janus.Storage.Authentication.Oidc;
using Npgsql;
using Xunit;

namespace Janus.Storage.Tests.Authentication;

/// <summary>
/// The stored signing keys: what the table admits, and that every change is written
/// only where the keys still stand as their caller read them (AUTH-KEY-001, D-166 X3).
/// </summary>
[Trait("kind", "integration")]
public sealed class SigningKeyStoreTests(DatabaseFixture database)
    : IClassFixture<DatabaseFixture>, IAsyncLifetime
{
    private static readonly DateTimeOffset Noon = new(2026, 9, 19, 12, 0, 0, TimeSpan.Zero);

    private static readonly TimeSpan Keeping = TimeSpan.FromDays(365);

    private readonly Deployment _deployment = new(database);

    /// <inheritdoc/>
    public async ValueTask InitializeAsync()
    {
        await using NpgsqlConnection connection = await database.OpenAsync();

        _ = await connection.ExecuteAsync("DELETE FROM identity.signing_keys;");
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        _deployment.Dispose();

        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// AUTH-KEY-001 AC7: the table admits one next key and one current key; a second of
    /// either is not written, whether the store is asked or a statement is written by
    /// hand.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_KEY_001_AC7_TheTableAdmitsOneNextKeyAndOneCurrentKeyAsync()
    {
        Assert.True(await AddedAsync(Made(first: true, Noon)));
        Assert.False(await AddedAsync(Made(first: true, Noon)));
        Assert.True(await AddedAsync(Made(first: false, Noon)));
        Assert.False(await AddedAsync(Made(first: false, Noon)));

        await using NpgsqlConnection connection = await database.OpenAsync();

        PostgresException refused = await Assert.ThrowsAsync<PostgresException>(async () =>
            await connection.ExecuteAsync(
                """
                INSERT INTO identity.signing_keys
                    (key_id, algorithm, public_key, private_key, created_at, signing_from, longest_lifetime)
                VALUES ('by-hand', 'ES256', '\x00', '\x00', @Noon, @Noon, interval '0');
                """,
                new { Noon }));

        Assert.Equal(PostgresErrorCodes.UniqueViolation, refused.SqlState);
        Assert.Equal(2, (await HeldAsync()).Count);
    }

    /// <summary>
    /// AUTH-KEY-001 AC7: the next key is made current only where the current key still
    /// stands as read, the longest lifetime it carries included, and the next key is
    /// still next; the replacement stores the end of the overlap and of the keeping.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_KEY_001_AC7_APromotionIsWrittenOnlyWhereTheKeysStandAsReadAsync()
    {
        SigningKey current = Made(first: true, Noon);
        SigningKey next = Made(first: false, Noon.AddDays(89));
        DateTimeOffset at = Noon.AddDays(90);

        _ = await AddedAsync(current);
        _ = await AddedAsync(next);

        await using (StoreContext lengthening = database.Context())
        {
            Assert.True(await Keys(lengthening).LengthenAsync(current, TimeSpan.FromMinutes(10), Cancellation));
        }

        await using (StoreContext stale = database.Context())
        {
            Assert.False(await Keys(stale).PromoteAsync(next, current, at, at, at + Keeping, Cancellation));
        }

        SigningKey read = (await HeldAsync()).Single(key => key.KeyId == current.KeyId);

        Assert.True(read.IsCurrent);
        Assert.True((await HeldAsync()).Single(key => key.KeyId == next.KeyId).IsNext);

        await using (StoreContext promoting = database.Context())
        {
            Assert.True(await Keys(promoting).PromoteAsync(
                next,
                read,
                at,
                read.OverlapEnd(at),
                at + Keeping,
                Cancellation));
        }

        IReadOnlyList<SigningKey> held = await HeldAsync();
        SigningKey replaced = held.Single(key => key.KeyId == current.KeyId);

        Assert.Equal(at, replaced.ReplacedAt);
        Assert.Equal(at + TimeSpan.FromMinutes(15), replaced.OverlapEndsAt);
        Assert.Equal(at + Keeping, replaced.KeptUntil);
        Assert.Equal(at, held.Single(key => key.KeyId == next.KeyId).SigningFrom);

        await using StoreContext again = database.Context();

        Assert.False(await Keys(again).PromoteAsync(next, read, at, at, at + Keeping, Cancellation));
    }

    /// <summary>
    /// AUTH-KEY-001 AC2 and AC7: the longest lifetime is never lowered, and a longer one
    /// is refused once the key is no longer current.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_KEY_001_AC7_ALongerLifetimeIsNeverLoweredAndRefusedOnceTheKeyIsReplacedAsync()
    {
        SigningKey current = Made(first: true, Noon);
        SigningKey next = Made(first: false, Noon.AddDays(89));
        DateTimeOffset at = Noon.AddDays(90);

        _ = await AddedAsync(current);
        _ = await AddedAsync(next);

        await using (StoreContext lengthening = database.Context())
        {
            Assert.True(await Keys(lengthening).LengthenAsync(current, TimeSpan.FromMinutes(20), Cancellation));
            Assert.True(await Keys(lengthening).LengthenAsync(current, TimeSpan.FromMinutes(10), Cancellation));
        }

        SigningKey read = (await HeldAsync()).Single(key => key.KeyId == current.KeyId);

        Assert.Equal(TimeSpan.FromMinutes(20), read.LongestLifetime);

        await using (StoreContext promoting = database.Context())
        {
            Assert.True(await Keys(promoting).PromoteAsync(next, read, at, read.OverlapEnd(at), at + Keeping, Cancellation));
        }

        await using (StoreContext late = database.Context())
        {
            Assert.False(await Keys(late).LengthenAsync(read, TimeSpan.FromMinutes(40), Cancellation));
        }

        Assert.Equal(
            TimeSpan.FromMinutes(20),
            (await HeldAsync()).Single(key => key.KeyId == current.KeyId).LongestLifetime);
    }

    /// <summary>
    /// AUTH-KEY-001 AC5 and AC6: a retirement removes the private key only once the
    /// overlap has ended, and a removal takes the key only once its keeping has ended.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_KEY_001_AC5_RetirementAndRemovalWaitForTheirTimesAsync()
    {
        SigningKey current = Made(first: true, Noon);
        SigningKey next = Made(first: false, Noon.AddDays(89));
        DateTimeOffset at = Noon.AddDays(90);

        _ = await AddedAsync(current);
        _ = await AddedAsync(next);

        await using (StoreContext promoting = database.Context())
        {
            _ = await Keys(promoting).PromoteAsync(next, current, at, current.OverlapEnd(at), at + Keeping, Cancellation);
        }

        SigningKey replaced = (await HeldAsync()).Single(key => key.KeyId == current.KeyId);
        DateTimeOffset overlapEnds = replaced.OverlapEndsAt!.Value;

        await using (StoreContext early = database.Context())
        {
            Assert.False(await Keys(early).RetireAsync(replaced, overlapEnds.AddSeconds(-1), Cancellation));
            Assert.False(await Keys(early).RemoveAsync(replaced, at + Keeping - TimeSpan.FromSeconds(1), Cancellation));
        }

        Assert.True((await HeldAsync()).Single(key => key.KeyId == current.KeyId).HoldsPrivateKey);

        await using (StoreContext retiring = database.Context())
        {
            Assert.True(await Keys(retiring).RetireAsync(replaced, overlapEnds, Cancellation));

            Assert.Null(await Keys(retiring).PrivateKeyAsync(current.KeyId, Cancellation));
        }

        Assert.False((await HeldAsync()).Single(key => key.KeyId == current.KeyId).HoldsPrivateKey);

        await using (StoreContext removing = database.Context())
        {
            Assert.True(await Keys(removing).RemoveAsync(replaced, at + Keeping, Cancellation));
        }

        Assert.DoesNotContain(await HeldAsync(), key => key.KeyId == current.KeyId);
    }

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private static SigningKey Made(bool first, DateTimeOffset at)
    {
        using var created = ECDsa.Create(ECCurve.NamedCurves.nistP256);

        byte[] publicKey = created.ExportSubjectPublicKeyInfo();
        string keyId = Convert.ToHexString(SHA256.HashData(publicKey))[..32];

        return first
            ? SigningKey.First(keyId, "ES256", publicKey, at)
            : SigningKey.Next(keyId, "ES256", publicKey, at);
    }

    private SigningKeyStore Keys(StoreContext context) =>
        new(context, new DataConnections(context), _deployment.DataKey(context));

    private async Task<bool> AddedAsync(SigningKey key)
    {
        await using StoreContext writing = database.Context();

        return await Keys(writing).AddAsync(key, RandomNumberGenerator.GetBytes(32), Cancellation);
    }

    private async Task<IReadOnlyList<SigningKey>> HeldAsync()
    {
        await using StoreContext reading = database.Context();

        return await Keys(reading).HeldAsync(Cancellation);
    }
}
