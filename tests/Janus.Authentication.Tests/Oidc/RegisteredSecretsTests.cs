using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Janus.Authentication.Oidc;
using Janus.Core;
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

    private static readonly DateTimeOffset Noon = new(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);

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

    private static string Read(Result<byte[]> current) =>
        Encoding.UTF8.GetString(current.Match(
            read => read,
            error => throw new Xunit.Sdk.XunitException($"The secret was refused: {error.Code}.")));

    // A client whose secret was issued long enough ago to have reached the cadence.
    private Task RegisteredAsync() =>
        _clients
            .AddAsync(
                new OidcClient(Client, Client, OidcClientKind.BrowserApplication, "https://app.example.test/return", ["openid"]),
                Encoding.UTF8.GetBytes("the-secret-first-issued"),
                DateTimeOffset.MinValue,
                TestContext.Current.CancellationToken)
            .AsTask();
}
