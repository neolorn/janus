using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Janus.Authentication.Oidc;
using Janus.Authentication.Tests;
using Janus.Core;
using Janus.Core.Configuration;
using Janus.Hosting.Bff;
using Janus.Hosting.Tests.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using Xunit;

namespace Janus.Hosting.Tests.Oidc;

/// <summary>
/// The signing keys changed by two processes over one database, and by a request whose
/// own unit of work rolls back (AUTH-KEY-001 AC7, D-166 X3).
/// </summary>
[Trait("kind", "integration")]
public sealed class SigningKeyConcurrencyTests(HostFixture host) : IClassFixture<HostFixture>, IAsyncLifetime
{
    private static readonly DateTimeOffset Noon = new(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);

    private static readonly TimeSpan Cadence = TimeSpan.FromDays(90);

    private static readonly TimeSpan Lead = TimeSpan.FromMinutes(5);

    /// <inheritdoc/>
    public async ValueTask InitializeAsync()
    {
        await using NpgsqlConnection connection = await host.OpenAsync();

        _ = await connection.ExecuteAsync(
            """
            DELETE FROM identity.signing_keys;
            DELETE FROM identity.settings WHERE key = 'oidc.accesstoken.lifetime';
            """);
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    /// <summary>
    /// AUTH-KEY-001 AC7: of two processes that together find the database without a
    /// key, one makes it current, and both then hold the set the stored keys give.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_KEY_001_AC7_OfTwoProcessesStartingOnAnEmptyDatabaseOneMakesTheKeyAsync()
    {
        var clock = new FixedClock(Noon);

        await using ServiceProvider one = Process(clock);
        await using ServiceProvider two = Process(clock);

        SigningKeySet[] read = await Task.WhenAll(
            Task.Run(() => ReadAsync(one), TestContext.Current.CancellationToken),
            Task.Run(() => ReadAsync(two), TestContext.Current.CancellationToken));

        (string KeyId, bool IsNext, bool IsCurrent, TimeSpan LongestLifetime) stored = Assert.Single(await StoredAsync());

        Assert.True(stored.IsCurrent);
        Assert.All(read, set => Assert.Equal(stored.KeyId, set.Signing.Key.KeyId));
    }

    /// <summary>
    /// AUTH-KEY-001 AC7: of two processes that together find the same change due, the
    /// next key made and then the next key made current, one makes each change, and
    /// both then hold the set the stored keys give.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_KEY_001_AC7_OfTwoProcessesFindingTheSameChangeDueOneMakesItAsync()
    {
        var clock = new FixedClock(Noon);

        await using ServiceProvider one = Process(clock);
        await using ServiceProvider two = Process(clock);

        _ = await ReadAsync(one);
        _ = await ReadAsync(two);

        clock.Advance(Cadence - Lead);

        SigningKeySet[] published = await BothAsync(one, two);
        IReadOnlyList<(string KeyId, bool IsNext, bool IsCurrent, TimeSpan LongestLifetime)> made = await StoredAsync();

        Assert.Equal(2, made.Count);
        Assert.Single(made, key => key.IsNext);
        Assert.All(published, set => Assert.Equal(2, set.Published(clock.GetUtcNow()).Count));

        clock.Advance(Lead);

        SigningKeySet[] rotated = await BothAsync(one, two);
        IReadOnlyList<(string KeyId, bool IsNext, bool IsCurrent, TimeSpan LongestLifetime)> changed = await StoredAsync();

        Assert.Equal(2, changed.Count);
        Assert.DoesNotContain(changed, key => key.IsNext);
        Assert.All(
            rotated,
            set => Assert.Equal(made.Single(key => key.IsNext).KeyId, set.Signing.Key.KeyId));
    }

    /// <summary>
    /// AUTH-KEY-001 AC7: a change, and a longer lifetime stored, during a request whose
    /// own unit of work rolls back stay made, since each commits in a transaction of its
    /// own.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_KEY_001_AC7_AChangeDuringARequestThatRollsBackStaysMadeAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        var clock = new FixedClock(Noon);

        await using ServiceProvider process = Process(clock);

        _ = await ReadAsync(process);

        clock.Advance(Cadence - Lead);
        await LifetimeAsync("PT20M");

        await using (AsyncServiceScope request = process.CreateAsyncScope())
        {
            IUnitOfWork work = request.ServiceProvider.GetRequiredService<IUnitOfWork>();
            IConfigurationStore configuration = request.ServiceProvider.GetRequiredService<IConfigurationStore>();
            SigningCredentialSource source = process.GetRequiredService<SigningCredentialSource>();

            Assert.True((await work.BeginAsync(cancellationToken)).Match(() => true, _ => false));

            _ = await source.ReadAsync(configuration, cancellationToken);
            _ = await source.SigningAsync(configuration, accessToken: true, cancellationToken);
        }

        IReadOnlyList<(string KeyId, bool IsNext, bool IsCurrent, TimeSpan LongestLifetime)> stored = await StoredAsync();

        Assert.Single(stored, key => key.IsNext);
        Assert.Equal(TimeSpan.FromMinutes(20), stored.Single(key => key.IsCurrent).LongestLifetime);
    }

    /// <summary>
    /// AUTH-KEY-001 AC7: a longer lifetime stored against a key another process has just
    /// replaced is refused, and the token is signed by the current key, which carries
    /// the longer lifetime in turn. The second process's clock runs a minute behind, so
    /// the times its set carries do not yet show the replacement.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_KEY_001_AC7_ALongerLifetimeAgainstAKeyAnotherProcessReplacedIsRefusedAsync()
    {
        var clock = new FixedClock(Noon);
        var behind = new FixedClock(Noon - TimeSpan.FromMinutes(1));

        await using ServiceProvider one = Process(clock);
        await using ServiceProvider two = Process(behind);

        string first = (await ReadAsync(one)).Signing.Key.KeyId;

        clock.Advance(Cadence - Lead);
        behind.Advance(Cadence - Lead + TimeSpan.FromMinutes(1));

        string next = (await ReadAsync(one)).Keys.Single(held => held.Key.IsNext).Key.KeyId;

        Assert.Contains((await ReadAsync(two)).Keys, held => held.Key.KeyId == next);

        clock.Advance(Lead);
        behind.Advance(Lead - TimeSpan.FromMinutes(2));

        Assert.Equal(next, (await ReadAsync(one)).Signing.Key.KeyId);

        await LifetimeAsync("PT20M");

        SigningCredentials signed = await SigningAsync(two);
        IReadOnlyList<(string KeyId, bool IsNext, bool IsCurrent, TimeSpan LongestLifetime)> stored = await StoredAsync();

        Assert.Equal(next, signed.Key.KeyId);
        Assert.Equal(TimeSpan.Zero, stored.Single(key => key.KeyId == first).LongestLifetime);
        Assert.Equal(TimeSpan.FromMinutes(20), stored.Single(key => key.KeyId == next).LongestLifetime);
    }

    private static async Task<SigningKeySet[]> BothAsync(ServiceProvider one, ServiceProvider two) =>
        await Task.WhenAll(
            Task.Run(() => ReadAsync(one), TestContext.Current.CancellationToken),
            Task.Run(() => ReadAsync(two), TestContext.Current.CancellationToken));

    private static async Task<SigningKeySet> ReadAsync(ServiceProvider process)
    {
        await using AsyncServiceScope scope = process.CreateAsyncScope();

        return (await process.GetRequiredService<SigningCredentialSource>().ReadAsync(
                scope.ServiceProvider.GetRequiredService<IConfigurationStore>(),
                TestContext.Current.CancellationToken))
            .Match(set => set, error => throw new InvalidOperationException(error.Code.ToString()));
    }

    private static async Task<SigningCredentials> SigningAsync(ServiceProvider process)
    {
        await using AsyncServiceScope scope = process.CreateAsyncScope();

        return (await process.GetRequiredService<SigningCredentialSource>().SigningAsync(
                scope.ServiceProvider.GetRequiredService<IConfigurationStore>(),
                accessToken: true,
                TestContext.Current.CancellationToken))
            .Match(signing => signing, error => throw new InvalidOperationException(error.Code.ToString()));
    }

    // One process of the deployment: its own container and credential source over the
    // fixture's database, on the clock given.
    private ServiceProvider Process(TimeProvider clock)
    {
        var services = new ServiceCollection();

        services.AddSingleton(clock);
        services.AddSingleton<ISecretSource>(HostFixture.Secrets(host.MaintenanceConnectionString));
        services.AddJanus(host.ConnectionString, HostFixture.Declaration(), ApplicationKind.Public);

        return HostFixture.Started(services.BuildServiceProvider());
    }

    private async Task LifetimeAsync(string lifetime)
    {
        await using NpgsqlConnection connection = await host.OpenAsync();

        _ = await connection.ExecuteAsync(
            "INSERT INTO identity.settings (key, value) VALUES ('oidc.accesstoken.lifetime', @lifetime) "
                + "ON CONFLICT (key) DO UPDATE SET value = excluded.value;",
            new { lifetime });
    }

    private async Task<IReadOnlyList<(string KeyId, bool IsNext, bool IsCurrent, TimeSpan LongestLifetime)>> StoredAsync()
    {
        await using NpgsqlConnection connection = await host.OpenAsync();

        return [.. await connection.QueryAsync<(string KeyId, bool IsNext, bool IsCurrent, TimeSpan LongestLifetime)>(
            "SELECT key_id, is_next, is_current, longest_lifetime FROM identity.signing_keys")];
    }
}
