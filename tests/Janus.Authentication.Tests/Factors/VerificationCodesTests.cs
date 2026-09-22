using System;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Janus.Authentication.Factors;
using Janus.Core;
using Janus.Core.Configuration;
using Xunit;

namespace Janus.Authentication.Tests.Factors;

/// <summary>
/// The verification codes: their own storage, their own lifetime, the tries that end
/// one and the single use that spends it (AUTH-FACT-004).
/// </summary>
[Trait("kind", "unit")]
public sealed class VerificationCodesTests : IAsyncDisposable
{
    private static readonly DateTimeOffset Noon = new(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);

    private static readonly byte[] Holder = [1, 2, 3, 4];

    private readonly VerificationCodeStoreInMemory _codes = new();
    private readonly AuthenticatorStoreInMemory _authenticators = new();
    private readonly ConfigurationInMemory _configuration = new();
    private readonly UnitOfWorkInMemory _work = new();
    private readonly FixedClock _clock = new(Noon);
    private readonly RandomNumberGenerator _randomness = RandomNumberGenerator.Create();

    /// <summary>
    /// AUTH-FACT-004 AC2: a code is held where codes are held and nowhere a credential
    /// is, and it dies on <c>code.verification.lifetime</c> whatever issued it.
    /// </summary>
    [Fact]
    public async Task AUTH_FACT_004_AC2_ACodeIsHeldApartAndLivesItsOwnLifetimeAsync()
    {
        _configuration.Set(Settings.CodeVerificationLifetime, TimeSpan.FromMinutes(5));

        string code = Drawn(await Service.IssueAsync(Holder, TestContext.Current.CancellationToken));

        VerificationCode held = Assert.Single(_codes.All);

        Assert.Equal(Noon + TimeSpan.FromMinutes(5), held.ExpiresAt);
        Assert.Empty(_authenticators.All);

        _clock.Advance(TimeSpan.FromMinutes(6));

        Assert.Equal(ErrorCodes.CodeExpired, await RefusalAsync(code));
    }

    /// <summary>
    /// AUTH-FACT-004 AC2: what the deployment configures is what the code lives, so
    /// two deployments do not share an expiry the library chose.
    /// </summary>
    [Fact]
    public async Task AUTH_FACT_004_AC2_TheLifetimeIsWhatTheDeploymentConfiguresAsync()
    {
        _configuration.Set(Settings.CodeVerificationLifetime, TimeSpan.FromMinutes(20));

        _ = Drawn(await Service.IssueAsync(Holder, TestContext.Current.CancellationToken));

        Assert.Equal(Noon + TimeSpan.FromMinutes(20), Assert.Single(_codes.All).ExpiresAt);
    }

    /// <summary>
    /// AUTH-FACT-004 AC3: the try that reaches the cap ends the code, the right code
    /// after it is refused, and the replacement leaves the dead one dead.
    /// </summary>
    [Fact]
    public async Task AUTH_FACT_004_AC3_TheCapEndsTheCodeAndAReplacementLeavesItDeadAsync()
    {
        _configuration.Set(Settings.CodeVerificationAttempts, 5);

        string right = Drawn(await Service.IssueAsync(Holder, TestContext.Current.CancellationToken));

        for (int attempt = 0; attempt < 4; attempt++)
        {
            Assert.Equal(ErrorCodes.CodeInvalid, await RefusalAsync(Wrong(right)));
        }

        Assert.Equal(ErrorCodes.CodeExpired, await RefusalAsync(Wrong(right)));
        Assert.Equal(ErrorCodes.CodeExpired, await RefusalAsync(right));
        Assert.Empty(_codes.All);

        string replacement =
            Drawn(await Service.IssueAsync(Holder, TestContext.Current.CancellationToken));

        Assert.Equal(ErrorCodes.CodeInvalid, await RefusalAsync(Wrong(replacement)));
        Assert.Null(await RefusalAsync(replacement));
    }

    /// <summary>
    /// AUTH-FACT-004 AC3: the right code is spent by the try that answered it, so the
    /// same digits never answer twice.
    /// </summary>
    [Fact]
    public async Task AUTH_FACT_004_AC3_TheRightCodeIsSpentOnceAsync()
    {
        string right = Drawn(await Service.IssueAsync(Holder, TestContext.Current.CancellationToken));

        Assert.Null(await RefusalAsync(right));
        Assert.Empty(_codes.All);
        Assert.Equal(ErrorCodes.CodeExpired, await RefusalAsync(right));
    }

    /// <summary>
    /// AUTH-FACT-004: a fresh code displaces whatever the holder had outstanding, so a
    /// person who asks for another has one code and not two.
    /// </summary>
    [Fact]
    public async Task IssueAsync_ACodeIsOutstanding_ReplacesItAsync()
    {
        string first = Drawn(await Service.IssueAsync(Holder, TestContext.Current.CancellationToken));
        string second = Drawn(await Service.IssueAsync(Holder, TestContext.Current.CancellationToken));

        _ = Assert.Single(_codes.All);

        if (!string.Equals(first, second, StringComparison.Ordinal))
        {
            Assert.Equal(ErrorCodes.CodeInvalid, await RefusalAsync(first));
        }

        Assert.Null(await RefusalAsync(second));
    }

    /// <summary>
    /// AUTH-FACT-004: nothing is outstanding until something is issued, so a code
    /// presented against a holder that has none is refused.
    /// </summary>
    [Fact]
    public async Task PresentAsync_NoCodeIsOutstanding_RefusesAsync()
    {
        Assert.Equal(ErrorCodes.CodeExpired, await RefusalAsync("000000"));
        Assert.False(await Service.OutstandingAsync(Holder, TestContext.Current.CancellationToken));
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await _work.DisposeAsync();

        _randomness.Dispose();

        GC.SuppressFinalize(this);
    }

    private VerificationCodes Service =>
        new(_codes, _configuration, _work, _clock, _randomness);

    // Any six digits other than the ones outstanding.
    private static string Wrong(string right) =>
        string.Equals(right, "000000", StringComparison.Ordinal) ? "111111" : "000000";

    private static string Drawn(Result<string> issued) => issued.Match(
        code => code,
        error => throw new Xunit.Sdk.XunitException("The code was not issued: " + error.Code));

    private async Task<ErrorCode?> RefusalAsync(string entered) =>
        (await Service.PresentAsync(Holder, entered, TestContext.Current.CancellationToken))
            .Match<ErrorCode?>(() => null, error => error.Code);
}
