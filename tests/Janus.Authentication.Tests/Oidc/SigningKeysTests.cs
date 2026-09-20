using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Janus.Authentication.Oidc;
using Janus.Core;
using Janus.Core.Configuration;
using Xunit;

namespace Janus.Authentication.Tests.Oidc;

/// <summary>
/// The keys the provider signs with: how one takes over from another, how long the one
/// before it stays published, and what leaves the set (AUTH-KEY-001, AUTH-KEY-003).
/// </summary>
[Trait("kind", "unit")]
public sealed class SigningKeysTests : IAsyncDisposable
{
    private static readonly DateTimeOffset Noon = new(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly SigningKeyStoreInMemory _store = new();
    private readonly ConfigurationInMemory _configuration = new();
    private readonly UnitOfWorkInMemory _work = new();
    private readonly FixedClock _clock = new(Noon);

    /// <inheritdoc/>
    public async ValueTask DisposeAsync() => await _work.DisposeAsync();

    /// <summary>
    /// AUTH-KEY-001 AC1: the first request signs, and a request made after the cadence
    /// has passed signs with a key nobody created by hand.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_KEY_001_AC1_RotationNeedsNoRestartAndNoPersonAsync()
    {
        string first = (await Keys.SigningAsync(TestContext.Current.CancellationToken)).Match(
            material => material.KeyId,
            error => throw new InvalidOperationException(error.Code.ToString()));

        _clock.Advance(TimeSpan.FromDays(91));

        string second = (await Keys.SigningAsync(TestContext.Current.CancellationToken)).Match(
            material => material.KeyId,
            error => throw new InvalidOperationException(error.Code.ToString()));

        Assert.NotEqual(first, second);
    }

    /// <summary>
    /// AUTH-KEY-001 AC2: the key that stopped signing stays published for the
    /// access-token lifetime and five minutes, so a token it signed still validates.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_KEY_001_AC2_ThePreviousKeyIsPublishedThroughTheOverlapAsync()
    {
        string first = await SigningAsync();

        _clock.Advance(TimeSpan.FromDays(91));

        _ = await SigningAsync();
        _clock.Advance(TimeSpan.FromMinutes(14));

        Assert.Contains(first, await PublishedAsync());
    }

    /// <summary>
    /// AUTH-KEY-001 AC3: once the overlap has passed, the previous key is gone from the
    /// set and a token it signed validates against nothing.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_KEY_001_AC3_ThePreviousKeyLeavesTheSetAfterTheOverlapAsync()
    {
        string first = await SigningAsync();

        _clock.Advance(TimeSpan.FromDays(91));

        _ = await SigningAsync();
        _clock.Advance(TimeSpan.FromMinutes(16));

        Assert.DoesNotContain(first, await PublishedAsync());
    }

    /// <summary>
    /// AUTH-KEY-001 AC4: every published key carries the configured algorithm, so a
    /// relying party that accepts that one accepts all of them.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_KEY_001_AC4_EveryPublishedKeyCarriesTheConfiguredAlgorithmAsync()
    {
        _ = await SigningAsync();

        IReadOnlyList<PublishedSigningKey> published = (await Keys
                .PublishedAsync(TestContext.Current.CancellationToken))
            .Match(keys => keys, error => throw new InvalidOperationException(error.Code.ToString()));

        Assert.NotEmpty(published);
        Assert.All(published, key => Assert.Equal("ES256", key.Algorithm));
    }

    /// <summary>
    /// AUTH-KEY-001 AC4: what the store is handed is the private material, and what the
    /// set publishes is the public half alone.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_KEY_001_AC4_ThePublishedSetCarriesNoPrivateMaterialAsync()
    {
        string keyId = await SigningAsync();

        byte[] privateKey = (await _store.PrivateKeyAsync(keyId, TestContext.Current.CancellationToken))!;

        IReadOnlyList<PublishedSigningKey> published = (await Keys
                .PublishedAsync(TestContext.Current.CancellationToken))
            .Match(keys => keys, error => throw new InvalidOperationException(error.Code.ToString()));

        using var held = ECDsa.Create();

        held.ImportPkcs8PrivateKey(privateKey, out _);

        Assert.Equal(
            Convert.ToHexString(held.ExportSubjectPublicKeyInfo()),
            Convert.ToHexString(published.Single(key => key.KeyId == keyId).PublicKey.ToArray()));
    }

    /// <summary>
    /// AUTH-KEY-003 AC1: what has left the published set is removed without a person
    /// asking for it.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_KEY_003_AC1_TheSweepRemovesWhatHasRetiredAsync()
    {
        _ = await SigningAsync();

        _clock.Advance(TimeSpan.FromDays(91));

        _ = await SigningAsync();
        _clock.Advance(TimeSpan.FromMinutes(16));

        Assert.Equal(1, await Keys.SweepAsync(TestContext.Current.CancellationToken));
        Assert.Equal(1, _store.Count);
    }

    private SigningKeys Keys => new(_store, _configuration, _work, _clock);

    private async Task<string> SigningAsync() =>
        (await Keys.SigningAsync(TestContext.Current.CancellationToken)).Match(
            material => material.KeyId,
            error => throw new InvalidOperationException(error.Code.ToString()));

    private async Task<IReadOnlyList<string>> PublishedAsync() =>
        [.. (await Keys.PublishedAsync(TestContext.Current.CancellationToken))
            .Match(keys => keys, error => throw new InvalidOperationException(error.Code.ToString()))
            .Select(key => key.KeyId)];
}
