using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Janus.Authentication.Factors;
using Janus.Core;
using Janus.Core.Configuration;
using Xunit;

namespace Janus.Authentication.Tests.Factors;

/// <summary>
/// The browsers an account knows: when its trust stands in for a second factor, when
/// it is taken away, and when a browser the account has not seen holds a sign-in
/// (AUTH-FACT-015, AUTH-FACT-016).
/// </summary>
[Trait("kind", "unit")]
public sealed class DeviceServiceTests : IAsyncDisposable
{
    private static readonly DateTimeOffset Noon =
        new(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);

    private static readonly Assurance TwoFactors =
        new(AssuranceLevel.Aal2, PhishingResistant: false);

    private static readonly Assurance OneFactor =
        new(AssuranceLevel.Aal1, PhishingResistant: false);

    private readonly DeviceStoreInMemory _devices = new();
    private readonly ConfigurationInMemory _configuration = new();
    private readonly UnitOfWorkInMemory _work = new();
    private readonly FixedClock _clock = new(Noon);
    private readonly RandomNumberGenerator _randomness = RandomNumberGenerator.Create();

    private DeviceService Service =>
        new(_devices, _configuration, _work, _clock, _randomness);

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await _work.DisposeAsync();
        _randomness.Dispose();
    }

    /// <summary>
    /// AUTH-FACT-015 AC1: a customer who signed in with two factors and trusted the
    /// browser is asked for the password only on it next time.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task AUTH_FACT_015_AC1_ATrustedBrowserSkipsTheSecondFactorAsync()
    {
        SubjectId subject = Subject();
        OpaqueToken trust = await TrustedAsync(subject);

        Assert.True(await Service.TrustsAsync(
            subject,
            trust.Value,
            TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// AUTH-FACT-015: the trust is one account's and stands for no other, and a
    /// browser carrying nothing is trusted by nothing.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task AUTH_FACT_015_TheTrustIsOneAccountsAloneAsync()
    {
        OpaqueToken trust = await TrustedAsync(Subject());

        Assert.False(await Service.TrustsAsync(
            Subject(),
            trust.Value,
            TestContext.Current.CancellationToken));
        Assert.False(await Service.TrustsAsync(
            Subject(),
            null,
            TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// AUTH-FACT-015 AC2: the trust is not a factor and raises nothing, so a gate on
    /// the session that follows still asks for what the password alone did not reach.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task AUTH_FACT_015_AC2_TheTrustRaisesNothingOnTheSessionAsync()
    {
        SubjectId subject = Subject();
        OpaqueToken trust = await TrustedAsync(subject);

        Assert.True(await Service.TrustsAsync(
            subject,
            trust.Value,
            TestContext.Current.CancellationToken));

        Device held = Assert.Single(_devices.All);

        Assert.Equal(DeviceKind.Trusted, held.Kind);
        Assert.Equal(Noon, held.LastUsedAt);
    }

    /// <summary>
    /// AUTH-FACT-015 AC3: the offer is absent for a principal whose policy requires
    /// two factors.
    /// </summary>
    [Fact]
    public void AUTH_FACT_015_AC3_TheOfferIsAbsentUnderAPolicyRequiringTwoFactors()
    {
        Assert.False(DeviceService.MayTrust(
            Janus.Core.Policies.AdministrativeOrganization,
            TwoFactors,
            passwordMeetsSingleFactorFloor: true));
        Assert.True(DeviceService.MayTrust(
            Janus.Core.Policies.SystemDefault,
            TwoFactors,
            passwordMeetsSingleFactorFloor: true));
    }

    /// <summary>
    /// AUTH-FACT-015 AC7: the offer is withheld while the account's password is below
    /// the floor a password that stands alone has to meet.
    /// </summary>
    [Fact]
    public void AUTH_FACT_015_AC7_TheOfferIsWithheldBelowTheSingleFactorFloor() =>
        Assert.False(DeviceService.MayTrust(
            Janus.Core.Policies.SystemDefault,
            TwoFactors,
            passwordMeetsSingleFactorFloor: false));

    /// <summary>
    /// AUTH-FACT-015: the offer follows a sign-in that reached two factors and no
    /// other.
    /// </summary>
    [Fact]
    public void AUTH_FACT_015_TheOfferFollowsATwoFactorSignIn() =>
        Assert.False(DeviceService.MayTrust(
            Janus.Core.Policies.SystemDefault,
            OneFactor,
            passwordMeetsSingleFactorFloor: true));

    /// <summary>
    /// AUTH-FACT-015 AC4: signing out everywhere clears every trusted device and
    /// leaves the browsers the check remembers alone.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task AUTH_FACT_015_AC4_EveryTrustedDeviceGoesAtOnceAsync()
    {
        SubjectId subject = Subject();
        OpaqueToken trust = await TrustedAsync(subject);
        OpaqueToken remembered = Value(await Service.RememberAsync(
            subject,
            Browser(),
            TestContext.Current.CancellationToken));

        await Service.RevokeTrustAsync(subject, TestContext.Current.CancellationToken);

        Assert.False(await Service.TrustsAsync(
            subject,
            trust.Value,
            TestContext.Current.CancellationToken));
        Assert.True(await Service.RemembersAsync(
            subject,
            remembered.Value,
            TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// AUTH-FACT-015 AC5: a trusted device older than the lifetime is asked for the
    /// second factor again.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task AUTH_FACT_015_AC5_ATrustOlderThanItsLifetimeStandsForNothingAsync()
    {
        SubjectId subject = Subject();
        OpaqueToken trust = await TrustedAsync(subject);

        _clock.Advance(TimeSpan.FromDays(30) - TimeSpan.FromMinutes(1));

        Assert.True(await Service.TrustsAsync(
            subject,
            trust.Value,
            TestContext.Current.CancellationToken));

        _clock.Advance(TimeSpan.FromMinutes(2));

        Assert.False(await Service.TrustsAsync(
            subject,
            trust.Value,
            TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// AUTH-FACT-015 AC5: how long a trust lasts is what the deployment configures.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task AUTH_FACT_015_AC5_TheLifetimeIsWhatTheDeploymentConfiguresAsync()
    {
        _configuration.Set(Settings.FactorTrustedDeviceLifetime, TimeSpan.FromDays(2));

        SubjectId subject = Subject();
        OpaqueToken trust = await TrustedAsync(subject);

        _clock.Advance(TimeSpan.FromDays(3));

        Assert.False(await Service.TrustsAsync(
            subject,
            trust.Value,
            TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// AUTH-FACT-015 AC6: three failed sign-ins in a row on a trusted browser revoke
    /// its trust, and a successful one clears what came before it.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task AUTH_FACT_015_AC6_ThreeFailuresInARowRevokeTheTrustAsync()
    {
        SubjectId subject = Subject();
        OpaqueToken trust = await TrustedAsync(subject);

        await Service.FailedAsync(subject, trust.Value, TestContext.Current.CancellationToken);
        await Service.FailedAsync(subject, trust.Value, TestContext.Current.CancellationToken);

        Assert.True(await Service.TrustsAsync(
            subject,
            trust.Value,
            TestContext.Current.CancellationToken));

        await Service.FailedAsync(subject, trust.Value, TestContext.Current.CancellationToken);
        await Service.FailedAsync(subject, trust.Value, TestContext.Current.CancellationToken);
        await Service.FailedAsync(subject, trust.Value, TestContext.Current.CancellationToken);

        Assert.False(await Service.TrustsAsync(
            subject,
            trust.Value,
            TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// AUTH-FACT-015 AC6: how many failures in a row it takes is what the deployment
    /// configures.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task AUTH_FACT_015_AC6_TheFailureLimitIsWhatTheDeploymentConfiguresAsync()
    {
        _configuration.Set(Settings.FactorTrustedDeviceFailureLimit, 1);

        SubjectId subject = Subject();
        OpaqueToken trust = await TrustedAsync(subject);

        await Service.FailedAsync(subject, trust.Value, TestContext.Current.CancellationToken);

        Assert.False(await Service.TrustsAsync(
            subject,
            trust.Value,
            TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// AUTH-FACT-015, AUTH-FACT-016: each browser appears in the account's list under
    /// what it is known for, and removing one takes away what it stood for.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task AUTH_FACT_015_TheAccountListsAndRemovesWhatItKnowsAsync()
    {
        SubjectId subject = Subject();
        OpaqueToken trust = await TrustedAsync(subject);

        await Service.RememberAsync(subject, Browser(), TestContext.Current.CancellationToken);

        IReadOnlyList<DeviceSummary> listed = Value(await Service.ListAsync(
            AccessContext.Of(subject),
            TestContext.Current.CancellationToken));

        Assert.Equal(
            [DeviceKind.Trusted, DeviceKind.Remembered],
            listed.Select(entry => entry.Kind).Order());
        Assert.All(listed, entry => Assert.Equal("Firefox Linux", entry.Label.ToString()));

        DeviceSummary trusted = listed.First(entry => entry.Kind is DeviceKind.Trusted);

        Assert.Null(Refusal(await Service.RemoveAsync(
            AccessContext.Of(subject),
            trusted.Id,
            TestContext.Current.CancellationToken)));
        Assert.False(await Service.TrustsAsync(
            subject,
            trust.Value,
            TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// AUTH-FACT-015: one account removes none of another's browsers.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task RemoveAsync_ABrowserOfAnotherAccount_IsRefusedAsync()
    {
        SubjectId subject = Subject();
        await TrustedAsync(subject);

        DeviceSummary held = Assert.Single(Value(await Service.ListAsync(
            AccessContext.Of(subject),
            TestContext.Current.CancellationToken)));

        Assert.Equal(
            ErrorCodes.Denied,
            Refusal(await Service.RemoveAsync(
                AccessContext.Of(Subject()),
                held.Id,
                TestContext.Current.CancellationToken)));
    }

    /// <summary>
    /// AUTH-FACT-016 AC1: a password-only sign-in from a browser the account has not
    /// seen is held for the check.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task AUTH_FACT_016_AC1_AnUnseenBrowserHoldsAPasswordOnlySignInAsync() =>
        Assert.True(Value(await Service.ChecksAsync(
            Subject(),
            OneFactor,
            OneFactor,
            null,
            TestContext.Current.CancellationToken)));

    /// <summary>
    /// AUTH-FACT-016 AC2, AC7: the browser that passed the check, or that completed
    /// the step of registration standing for it, is not held again inside the
    /// remembered period, and is held again after it.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task AUTH_FACT_016_AC2_ARememberedBrowserIsNotHeldAgainAsync()
    {
        SubjectId subject = Subject();
        OpaqueToken browser = Value(await Service.RememberAsync(
            subject,
            Browser(),
            TestContext.Current.CancellationToken));

        Assert.False(Value(await Service.ChecksAsync(
            subject,
            OneFactor,
            OneFactor,
            browser.Value,
            TestContext.Current.CancellationToken)));

        _clock.Advance(TimeSpan.FromDays(91));

        Assert.True(Value(await Service.ChecksAsync(
            subject,
            OneFactor,
            OneFactor,
            browser.Value,
            TestContext.Current.CancellationToken)));
    }

    /// <summary>
    /// AUTH-FACT-016 AC3: a sign-in that reached two factors is never held, whichever
    /// browser it came from.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task AUTH_FACT_016_AC3_ATwoFactorSignInIsNeverHeldAsync()
    {
        Assert.False(Value(await Service.ChecksAsync(
            Subject(),
            OneFactor,
            TwoFactors,
            null,
            TestContext.Current.CancellationToken)));
        Assert.False(Value(await Service.ChecksAsync(
            Subject(),
            new Assurance(AssuranceLevel.Aal2, PhishingResistant: true),
            new Assurance(AssuranceLevel.Aal2, PhishingResistant: true),
            null,
            TestContext.Current.CancellationToken)));
    }

    /// <summary>
    /// AUTH-FACT-016: an account that can reach two factors protects itself and is
    /// never held for a code.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task AUTH_FACT_016_AnAccountReachingTwoFactorsIsNeverHeldAsync() =>
        Assert.False(Value(await Service.ChecksAsync(
            Subject(),
            TwoFactors,
            OneFactor,
            null,
            TestContext.Current.CancellationToken)));

    /// <summary>
    /// AUTH-FACT-016 AC6: with the check turned off, no sign-in is held.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task AUTH_FACT_016_AC6_WithTheCheckOffNoSignInIsHeldAsync()
    {
        _configuration.Set(Settings.DeviceVerificationEnabled, false);

        Assert.False(Value(await Service.ChecksAsync(
            Subject(),
            OneFactor,
            OneFactor,
            null,
            TestContext.Current.CancellationToken)));
    }

    /// <summary>
    /// AUTH-FACT-016 AC2: how long a browser is remembered is what the deployment
    /// configures.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task AUTH_FACT_016_AC2_TheRememberedPeriodIsWhatTheDeploymentConfiguresAsync()
    {
        _configuration.Set(Settings.DeviceVerificationLifetime, TimeSpan.FromDays(5));

        SubjectId subject = Subject();
        OpaqueToken browser = Value(await Service.RememberAsync(
            subject,
            Browser(),
            TestContext.Current.CancellationToken));

        _clock.Advance(TimeSpan.FromDays(6));

        Assert.True(Value(await Service.ChecksAsync(
            subject,
            OneFactor,
            OneFactor,
            browser.Value,
            TestContext.Current.CancellationToken)));
    }

    private static DeviceDescription Browser() => new("Firefox", "Linux");

    private static TValue Value<TValue>(Result<TValue> result) =>
        result.Match(value => value, error => throw new Xunit.Sdk.XunitException(error.Code.ToString()));

    private static ErrorCode? Refusal(Result result) =>
        result.Match<ErrorCode?>(() => null, error => error.Code);

    private SubjectId Subject() => SubjectId.New(_randomness);

    private async ValueTask<OpaqueToken> TrustedAsync(SubjectId subject) =>
        Value(await Service.TrustAsync(
            subject,
            Browser(),
            TestContext.Current.CancellationToken));
}
