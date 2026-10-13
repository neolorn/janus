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
/// The secret a registered client presents, replaced at the cadence of the signing
/// keys: what the unit of work of a replacement keeps, and what the one that lost the
/// race to another process leaves (OPS-SEC-002, CONV-DESIGN-003).
/// </summary>
[Trait("kind", "unit")]
public sealed class RegisteredSecretsTests : IAsyncDisposable
{
    private const string Client = "an-application";
    private const string First = "the-secret-first-issued";

    private static readonly DateTimeOffset Noon = new(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);

    // AUTH-KEY-001: how long a replaced secret is taken, the access-token lifetime and
    // five minutes, at the default the tests run under.
    private static readonly TimeSpan Overlap = Settings.OidcAccessTokenLifetime.Default + TimeSpan.FromMinutes(5);

    private readonly ConfigurationInMemory _configuration = new();
    private readonly OidcClientStoreInMemory _clients = new();
    private readonly UnitOfWorkInMemory _work = new();
    private readonly FixedClock _clock = new(Noon);
    private readonly RandomNumberGenerator _randomness = RandomNumberGenerator.Create();

    private RegisteredSecrets Secrets => new(_clients, _configuration, _work, _randomness, _clock);

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await _work.DisposeAsync();
        _randomness.Dispose();
    }

    /// <summary>
    /// CONV-DESIGN-003 AC10, OPS-SEC-002: a replacement another process made first
    /// answers the secret that process wrote, with this one's unit of work rolled back,
    /// since it wrote nothing.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task CONV_DESIGN_003_AC10_AReplacementAnotherProcessMadeFirstIsRolledBackAsync()
    {
        await RegisteredAsync();

        await using var other = new UnitOfWorkInMemory();
        string? written = null;

        _clients.BeforeReplacement = async cancellationToken =>
            written = Read(await new RegisteredSecrets(_clients, _configuration, other, _randomness, _clock)
                .CurrentAsync(Client, cancellationToken));

        string answered = Read(await Secrets.CurrentAsync(Client, TestContext.Current.CancellationToken));

        Assert.Equal(written, answered);
        Assert.Equal(1, _clients.Replacements);
        Assert.False(_work.Open);
        Assert.Equal((0, 1), (_work.Committed, _work.RolledBack));
        Assert.Equal((1, 0), (other.OutermostCommitted, other.RolledBack));
    }

    /// <summary>
    /// CONV-DESIGN-003 AC10, OPS-SEC-002: a replacement that was made commits it.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task CONV_DESIGN_003_AC10_AReplacementThatWasMadeCommitsAsync()
    {
        await RegisteredAsync();

        _ = Read(await Secrets.CurrentAsync(Client, TestContext.Current.CancellationToken));

        Assert.Equal(1, _clients.Replacements);
        Assert.False(_work.Open);
        Assert.Equal((1, 0), (_work.OutermostCommitted, _work.RolledBack));
    }

    /// <summary>
    /// OPS-SEC-002 AC3 (D-197): the replaced secret and the end of its overlap stay
    /// stored through the overlap, and the first read of the client's secrets at that
    /// end removes both, in a unit of work that commits.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task OPS_SEC_002_AC3_TheReplacedSecretIsRemovedByTheFirstReadAfterItsOverlapAsync()
    {
        await RegisteredAsync();
        string replacing = Read(await Secrets.CurrentAsync(Client, TestContext.Current.CancellationToken));
        _clock.Advance(Overlap - TimeSpan.FromSeconds(1));
        _ = Read(await Secrets.CurrentAsync(Client, TestContext.Current.CancellationToken));
        RegisteredClient within = Assert.Single(_clients.Registered);
        _clock.Advance(TimeSpan.FromSeconds(1));
        _work.Reset();

        string current = Read(await Secrets.CurrentAsync(Client, TestContext.Current.CancellationToken));

        RegisteredClient after = Assert.Single(_clients.Registered);
        Assert.Equal(Encoding.UTF8.GetBytes(First), within.Previous);
        Assert.Equal(Noon + Overlap, within.PreviousUntil);
        Assert.Null(after.Previous);
        Assert.Null(after.PreviousUntil);
        Assert.Equal((replacing, replacing), (current, Encoding.UTF8.GetString(after.Secret)));
        Assert.False(_work.Open);
        Assert.Equal((1, 0), (_work.OutermostCommitted, _work.RolledBack));
    }

    /// <summary>
    /// OPS-SEC-002 (D-197): the end of a replaced secret's overlap is stored at the
    /// replacement, and a change of the access-token lifetime made after it moves
    /// neither the end stored nor the read that removes the secret.
    /// </summary>
    /// <param name="minutes">The access-token lifetime set after the replacement.</param>
    /// <returns>The work of the test.</returns>
    [Theory]
    [InlineData(1)]
    [InlineData(60)]
    public async Task OPS_SEC_002_AChangeOfASettingDoesNotMoveTheEndOfAnOverlapAsync(int minutes)
    {
        await RegisteredAsync();
        _ = Read(await Secrets.CurrentAsync(Client, TestContext.Current.CancellationToken));
        _configuration.Set(Settings.OidcAccessTokenLifetime, TimeSpan.FromMinutes(minutes));
        _clock.Advance(Overlap - TimeSpan.FromSeconds(1));
        _ = Read(await Secrets.CurrentAsync(Client, TestContext.Current.CancellationToken));
        RegisteredClient within = Assert.Single(_clients.Registered);
        _clock.Advance(TimeSpan.FromSeconds(1));

        _ = Read(await Secrets.CurrentAsync(Client, TestContext.Current.CancellationToken));

        Assert.Equal(Encoding.UTF8.GetBytes(First), within.Previous);
        Assert.Equal(Noon + Overlap, within.PreviousUntil);
        Assert.Null(Assert.Single(_clients.Registered).Previous);
    }

    /// <summary>
    /// CONV-DESIGN-003 AC10, OPS-SEC-002 AC3: a removal another process made first
    /// leaves this read nothing to write, so its unit of work is rolled back and the
    /// current secret is answered all the same.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task CONV_DESIGN_003_AC10_ARemovalAnotherProcessMadeFirstIsRolledBackAsync()
    {
        await RegisteredAsync();
        string replacing = Read(await Secrets.CurrentAsync(Client, TestContext.Current.CancellationToken));
        _clock.Advance(Overlap);
        _work.Reset();
        _clients.BeforeRemoval = async cancellationToken =>
            _ = await _clients.RemoveReplacedSecretAsync(Client, _clock.GetUtcNow(), cancellationToken);

        string current = Read(await Secrets.CurrentAsync(Client, TestContext.Current.CancellationToken));

        Assert.Equal(replacing, current);
        Assert.Null(Assert.Single(_clients.Registered).Previous);
        Assert.False(_work.Open);
        Assert.Equal((0, 1), (_work.Committed, _work.RolledBack));
    }

    private static string Read(Result<byte[]> current) =>
        Encoding.UTF8.GetString(current.Match(
            read => read,
            error => throw new Xunit.Sdk.XunitException($"The secret was refused: {error.Code}.")));

    // A client whose secret was issued long enough ago to have reached the cadence.
    private Task RegisteredAsync() =>
        _clients
            .AddAsync(
                new OidcClient(Client, Client, OidcClientKind.BrowserApplication, "https://app.example.test/return", ["openid"]),
                Encoding.UTF8.GetBytes(First),
                DateTimeOffset.MinValue,
                TestContext.Current.CancellationToken)
            .AsTask();
}
