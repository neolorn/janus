using System;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Janus.Authentication.Factors;
using Janus.Authentication.Tests.Passwords;
using Janus.Core;
using OtpNet;
using Xunit;

namespace Janus.Authentication.Tests.Factors;

/// <summary>
/// Time-based one-time codes: the steps they are accepted at, the reuse they are
/// refused for, and the confirmation an enrolment needs (AUTH-FACT-005,
/// AUTH-FACT-007).
/// </summary>
[Trait("kind", "unit")]
public sealed class TotpServiceTests : IAsyncDisposable
{
    private static readonly DateTimeOffset Noon =
        new(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly AuthenticatorStoreInMemory _authenticators = new();
    private readonly PasswordStoreInMemory _passwords = new();
    private readonly ConfigurationInMemory _configuration = new();
    private readonly UnitOfWorkInMemory _work = new();
    private readonly FixedClock _clock = new(Noon);
    private readonly RandomNumberGenerator _randomness = RandomNumberGenerator.Create();

    private TotpService Service =>
        new(_authenticators, _passwords, _configuration, _work, _clock, _randomness);

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await _work.DisposeAsync();
        _randomness.Dispose();
    }

    /// <summary>
    /// AUTH-FACT-005 AC2: a code from one step ago is accepted.
    /// </summary>
    [Fact]
    public async Task AUTH_FACT_005_AC2_ACodeFromOneStepAgoIsAcceptedAsync()
    {
        SubjectId subject = Subject();
        TotpEnrolment enrolment = await EnrolledAsync(subject);

        Assert.Null(Refusal(await Service.PresentAsync(
            subject,
            Code(enrolment, Now - TimeSpan.FromSeconds(TotpCodes.StepSeconds)),
            TestContext.Current.CancellationToken)));
    }

    /// <summary>
    /// AUTH-FACT-005 AC1: a code from two steps ago is rejected.
    /// </summary>
    [Fact]
    public async Task AUTH_FACT_005_AC1_ACodeFromTwoStepsAgoIsRejectedAsync()
    {
        SubjectId subject = Subject();
        TotpEnrolment enrolment = await EnrolledAsync(subject);

        Assert.Equal(
            ErrorCodes.CodeInvalid,
            Refusal(await Service.PresentAsync(
                subject,
                Code(enrolment, Now - TimeSpan.FromSeconds(2 * TotpCodes.StepSeconds)),
                TestContext.Current.CancellationToken)));
    }

    /// <summary>
    /// AUTH-FACT-005 AC3: submitting the same valid code twice succeeds once and
    /// fails once.
    /// </summary>
    [Fact]
    public async Task AUTH_FACT_005_AC3_TheSameCodeTwiceSucceedsOnceAsync()
    {
        SubjectId subject = Subject();
        TotpEnrolment enrolment = await EnrolledAsync(subject);
        string code = Code(enrolment, Now);

        Assert.Null(Refusal(await Service.PresentAsync(
            subject,
            code,
            TestContext.Current.CancellationToken)));
        Assert.Equal(
            ErrorCodes.CodeReplayed,
            Refusal(await Service.PresentAsync(
                subject,
                code,
                TestContext.Current.CancellationToken)));
    }

    /// <summary>
    /// AUTH-FACT-005: the drift tolerance is what the deployment sets, and a
    /// deployment that narrows it to nothing accepts the current step only.
    /// </summary>
    [Fact]
    public async Task AUTH_FACT_005_TheDriftToleranceIsWhatTheDeploymentSetsAsync()
    {
        SubjectId subject = Subject();
        TotpEnrolment enrolment = await EnrolledAsync(subject);

        _configuration.Set(Janus.Core.Configuration.Settings.FactorTotpDrift, 0);

        Assert.Equal(
            ErrorCodes.CodeInvalid,
            Refusal(await Service.PresentAsync(
                subject,
                Code(enrolment, Now - TimeSpan.FromSeconds(TotpCodes.StepSeconds)),
                TestContext.Current.CancellationToken)));
    }

    /// <summary>
    /// AUTH-FACT-007: enrolment requires one valid code before the factor becomes
    /// active, so a mis-scanned secret cannot lock its owner out.
    /// </summary>
    [Fact]
    public async Task AUTH_FACT_007_AnUnconfirmedEnrolmentAuthenticatesNothingAsync()
    {
        SubjectId subject = Subject();
        TotpEnrolment enrolment = Value(await Service.BeginAsync(
            subject,
            Label(),
            TestContext.Current.CancellationToken));

        Assert.Equal(
            ErrorCodes.CodeInvalid,
            Refusal(await Service.PresentAsync(
                subject,
                Code(enrolment, Now),
                TestContext.Current.CancellationToken)));

        Result confirmed = await Service.ConfirmAsync(
            subject,
            enrolment.Id,
            Code(enrolment, Now),
            TestContext.Current.CancellationToken);

        Assert.Null(Refusal(confirmed));
        Assert.True(Assert.Single(_authenticators.All).IsUsable);
    }

    /// <summary>
    /// AUTH-FACT-007 AC1: an enrolment abandoned before confirmation leaves no active
    /// factor.
    /// </summary>
    [Fact]
    public async Task AUTH_FACT_007_AC1_AnAbandonedEnrolmentLeavesNoActiveFactorAsync()
    {
        SubjectId subject = Subject();
        TotpEnrolment enrolment = Value(await Service.BeginAsync(
            subject,
            Label(),
            TestContext.Current.CancellationToken));

        Assert.False(Assert.Single(_authenticators.All).IsUsable);

        Assert.Null(Refusal(await Service.AbandonAsync(
            subject,
            enrolment.Id,
            TestContext.Current.CancellationToken)));
        Assert.Empty(_authenticators.All);
    }

    /// <summary>
    /// AUTH-FACT-007: a confirmation refuses a wrong code and leaves the enrolment
    /// where it was.
    /// </summary>
    [Fact]
    public async Task ConfirmAsync_AWrongCode_LeavesTheEnrolmentUnconfirmedAsync()
    {
        SubjectId subject = Subject();
        TotpEnrolment enrolment = Value(await Service.BeginAsync(
            subject,
            Label(),
            TestContext.Current.CancellationToken));

        Result confirmed = await Service.ConfirmAsync(
            subject,
            enrolment.Id,
            "000000",
            TestContext.Current.CancellationToken);

        Assert.Equal(ErrorCodes.CodeInvalid, Refusal(confirmed));
        Assert.False(Assert.Single(_authenticators.All).IsUsable);
    }

    /// <summary>
    /// AUTH-FACT-007: a credential of another account is not one this account may
    /// confirm.
    /// </summary>
    [Fact]
    public async Task ConfirmAsync_ACredentialOfAnotherAccount_IsRefusedAsync()
    {
        TotpEnrolment theirs = Value(await Service.BeginAsync(
            Subject(),
            Label(),
            TestContext.Current.CancellationToken));

        Result confirmed = await Service.ConfirmAsync(
            Subject(),
            theirs.Id,
            Code(theirs, Now),
            TestContext.Current.CancellationToken);

        Assert.Equal(ErrorCodes.FactorRejected, Refusal(confirmed));
    }

    /// <summary>
    /// AUTH-FACT-005: a code of one account's generator is not a code of another's.
    /// </summary>
    [Fact]
    public async Task PresentAsync_ACodeOfAnotherAccountsGenerator_IsRefusedAsync()
    {
        SubjectId mine = Subject();
        SubjectId theirs = Subject();
        await EnrolledAsync(mine);
        TotpEnrolment enrolment = await EnrolledAsync(theirs);

        Assert.Equal(
            ErrorCodes.CodeInvalid,
            Refusal(await Service.PresentAsync(
                mine,
                Code(enrolment, Now),
                TestContext.Current.CancellationToken)));
    }

    private static CredentialLabel Label() =>
        CredentialLabel.Of(new DeviceDescription("Firefox", "Fedora"));

    private static string Code(TotpEnrolment enrolment, DateTimeOffset at) =>
        new Totp(
                enrolment.Secret.ToArray(),
                TotpCodes.StepSeconds,
                OtpHashMode.Sha1,
                TotpCodes.Digits)
            .ComputeTotp(at.UtcDateTime);

    private static TValue Value<TValue>(Result<TValue> result) =>
        result.Match(value => value, error => throw new Xunit.Sdk.XunitException(error.Code.ToString()));

    private static ErrorCode? Refusal(Result result) =>
        result.Match<ErrorCode?>(() => null, error => error.Code);

    private static ErrorCode? Refusal<TValue>(Result<TValue> result) =>
        result.Match<ErrorCode?>(_ => null, error => error.Code);

    private DateTimeOffset Now => _clock.GetUtcNow();

    /// <summary>
    /// AUTH-FACT-002b AC2: a code generator is a second step, so an account holding
    /// no password does not enrol one.
    /// </summary>
    [Fact]
    public async Task AUTH_FACT_002b_AC2_ACodeGeneratorIsRefusedWithoutAPasswordAsync()
    {
        SubjectId subject = Without();

        Assert.Equal(
            ErrorCodes.FactorNotPermitted,
            Refusal(await Service.BeginAsync(
                subject,
                Label(),
                TestContext.Current.CancellationToken)));

        Assert.Empty(await _authenticators.OfAsync(subject, TestContext.Current.CancellationToken));
    }

    // Every enrolment here is of a second step, which an account holds only once it
    // holds a password (AUTH-FACT-002b).
    private SubjectId Subject()
    {
        SubjectId subject = Without();

        _passwords.Hold(subject, Noon);

        return subject;
    }

    private SubjectId Without() => SubjectId.New(_randomness);

    private async ValueTask<TotpEnrolment> EnrolledAsync(SubjectId subject)
    {
        TotpEnrolment enrolment = Value(await Service.BeginAsync(
            subject,
            Label(),
            TestContext.Current.CancellationToken));

        Assert.Null(Refusal(await Service.ConfirmAsync(
            subject,
            enrolment.Id,
            Code(enrolment, Now),
            TestContext.Current.CancellationToken)));

        // The confirming code spent its step, so the clock moves past it and a test
        // presenting a code afterwards works from a step of its own.
        _clock.Advance(TimeSpan.FromSeconds(3 * TotpCodes.StepSeconds));

        return enrolment;
    }
}
