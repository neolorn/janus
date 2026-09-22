using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Janus.Authentication.Factors;
using Janus.Authentication.Passwords;
using Janus.Authentication.Policies;
using Janus.Authentication.Recovery;
using Janus.Authentication.Sending;
using Janus.Authentication.Tests.Accounts;
using Janus.Authentication.Tests.Factors;
using Janus.Authentication.Tests.Identifiers;
using Janus.Authentication.Tests.Passwords;
using Janus.Authentication.Tests.Policies;
using Janus.Authentication.Tests.Sending;
using Janus.Core;
using Janus.Core.Configuration;
using OtpNet;
using Xunit;

namespace Janus.Authentication.Tests.Recovery;

/// <summary>
/// Reporting a credential lost: refused at once, notified through the window,
/// cancellable from any notice, and gone only when the window has run and somebody
/// was told (AUTH-RECOV-007, AUTH-RECOV-007a, AUTH-RECOV-008).
/// </summary>
[Trait("kind", "unit")]
public sealed class LossReportsTests : IAsyncDisposable
{
    private const string Language = "en";
    private const string Source = "198.51.100.7";
    private const string Address = "person@example.test";
    private const string Number = "+441632960011";
    private const string Secret = "orangemarmaladeandtoast";
    private const string Short = "marmalade1";

    private static readonly DateTimeOffset Noon = new(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);

    private static readonly OrganizationId Staff = new(Guid.NewGuid());

    private readonly LossReportStoreInMemory _reports = new();
    private readonly IdentifierDirectoryInMemory _identifiers = new();
    private readonly AccountDirectoryInMemory _accounts = new(PreferenceDeclarations.None);
    private readonly AuthenticatorStoreInMemory _authenticators = new();
    private readonly PasswordStoreInMemory _passwords = new();
    private readonly LeakedPasswordCorpusInMemory _corpus = new();
    private readonly WordListInMemory _words = new();
    private readonly ScreeningLogInMemory _screening = new();
    private readonly RecoveryCodeStoreInMemory _sets = new();
    private readonly CredentialAuditInMemory _credentials = new();
    private readonly MembershipLookupInMemory _memberships = new();
    private readonly PolicyRaiseStoreInMemory _raises = new();
    private readonly ConfigurationInMemory _configuration = new();
    private readonly NotificationHandlerInMemory _notifications = new();
    private readonly UnitOfWorkInMemory _work = new();
    private readonly FixedClock _clock = new(Noon);
    private readonly RandomNumberGenerator _randomness = RandomNumberGenerator.Create();

    /// <summary>
    /// A deployment that can send the notices the window carries.
    /// </summary>
    public LossReportsTests() => _configuration.Set(Settings.AbuseSmsBalanceFloor, 0m);

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await _work.DisposeAsync();
        _randomness.Dispose();
    }

    /// <summary>
    /// AUTH-RECOV-007 AC1: a session holding nothing but a password reports the lost
    /// generator, no step-up is asked for, and it is refused from that instant.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_RECOV_007_AC1_APasswordAloneReportsALossAndSuspendsItAtOnceAsync()
    {
        SubjectId subject = await AccountAsync();
        AuthenticatorId generator = await EnrolledAsync(subject);

        LossReported? reported = Value(await Service.ReportAsync(
            AccessContext.Of(subject),
            generator,
            Source,
            TestContext.Current.CancellationToken));

        Assert.NotNull(reported);
        Assert.Equal(_clock.GetUtcNow() + TimeSpan.FromDays(7), reported.InvalidatesAt);
        Assert.Equal(AuthenticatorState.Suspended, await StateAsync(generator));
        Assert.NotEmpty(_notifications.Mail);
    }

    /// <summary>
    /// AUTH-RECOV-007 AC2: a suspended generator is no longer presentable, so a sign-in
    /// that offers its code is refused and a gate that would have counted it is not
    /// reached.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_RECOV_007_AC2_ASuspendedCredentialIsRejectedAtSignInAsync()
    {
        SubjectId subject = await AccountAsync();
        AuthenticatorId generator = await EnrolledAsync(subject);
        string code = Code(generator);

        _ = await Service.ReportAsync(
            AccessContext.Of(subject),
            generator,
            Source,
            TestContext.Current.CancellationToken);

        Assert.Equal(
            ErrorCodes.CodeInvalid,
            Refused(await Totp.PresentAsync(subject, code, TestContext.Current.CancellationToken)));

        Assert.DoesNotContain(Factor.Totp, await UsableAsync(subject));
    }

    /// <summary>
    /// AUTH-RECOV-007 AC4: while the report runs, the account still reaches what it
    /// reached before, so every gate that needed the lost factor stays closed to
    /// everyone; only invalidation moves it.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_RECOV_007_AC4_ReachableAssuranceMovesOnlyOnInvalidationAsync()
    {
        SubjectId subject = await AccountAsync();
        AuthenticatorId generator = await EnrolledAsync(subject);

        _ = await Service.ReportAsync(
            AccessContext.Of(subject),
            generator,
            Source,
            TestContext.Current.CancellationToken);

        Assert.Equal(AssuranceLevel.Aal2, await ReachableAsync(subject));

        _clock.Advance(TimeSpan.FromDays(8));

        _ = await Service.AdvanceAsync(TestContext.Current.CancellationToken);

        Assert.Equal(AssuranceLevel.Aal1, await ReachableAsync(subject));
    }

    /// <summary>
    /// AUTH-RECOV-007 AC3: invalidation waits for the window, and a cancellation inside
    /// it puts the credential back.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_RECOV_007_AC3_CancellingInsideTheWindowRestoresTheCredentialAsync()
    {
        SubjectId subject = await AccountAsync();
        AuthenticatorId generator = await EnrolledAsync(subject);

        _ = await Service.ReportAsync(
            AccessContext.Of(subject),
            generator,
            Source,
            TestContext.Current.CancellationToken);

        _clock.Advance(TimeSpan.FromDays(3));

        _ = await Service.AdvanceAsync(TestContext.Current.CancellationToken);

        Assert.Equal(AuthenticatorState.Suspended, await StateAsync(generator));

        Assert.True(Succeeded(await Service.CancelAsync(
            AccessContext.Of(subject),
            generator,
            cancelToken: null,
            TestContext.Current.CancellationToken)));

        Assert.Equal(AuthenticatorState.Active, await StateAsync(generator));

        _clock.Advance(TimeSpan.FromDays(8));

        _ = await Service.AdvanceAsync(TestContext.Current.CancellationToken);

        Assert.Equal(AuthenticatorState.Active, await StateAsync(generator));
    }

    /// <summary>
    /// AUTH-RECOV-007 AC3: the link every notice carried cancels the report on its own,
    /// which is what someone who cannot sign in has.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_RECOV_007_AC3_TheLinkInTheNoticeCancelsTheReportAsync()
    {
        SubjectId subject = await AccountAsync();
        AuthenticatorId generator = await EnrolledAsync(subject);

        _ = await Service.ReportAsync(
            AccessContext.Of(subject),
            generator,
            Source,
            TestContext.Current.CancellationToken);

        Assert.True(Succeeded(await Service.CancelAsync(
            AccessContext.Of(new SubjectId(Guid.NewGuid())),
            generator,
            _notifications.Mail[^1].Values["token"],
            TestContext.Current.CancellationToken)));

        Assert.Equal(AuthenticatorState.Active, await StateAsync(generator));
    }

    /// <summary>
    /// AUTH-RECOV-007 AC3: with every notice failing delivery, invalidation is held
    /// when the window ends and the report carries that it was.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_RECOV_007_AC3_InvalidationIsHeldWhereNoNoticeDeliveredAsync()
    {
        SubjectId subject = await AccountAsync();
        AuthenticatorId generator = await EnrolledAsync(subject);

        _notifications.Refusal = Error.From(ErrorCodes.SystemFault);

        _ = await Service.ReportAsync(
            AccessContext.Of(subject),
            generator,
            Source,
            TestContext.Current.CancellationToken);

        _clock.Advance(TimeSpan.FromDays(8));

        _ = await Service.AdvanceAsync(TestContext.Current.CancellationToken);

        Assert.Equal(AuthenticatorState.Suspended, await StateAsync(generator));
        Assert.NotNull(
            (await _reports.FindAsync(generator, TestContext.Current.CancellationToken))!.HeldAt);
    }

    /// <summary>
    /// AUTH-RECOV-007 AC6: invalidating the last second factor takes the recovery code
    /// set with it, because there is nothing left for the codes to stand in for.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_RECOV_007_AC6_TheLastSecondFactorTakesTheRecoveryCodesAsync()
    {
        SubjectId subject = await AccountAsync();
        AuthenticatorId generator = await EnrolledAsync(subject);

        _ = Value(await Codes.GenerateAsync(subject, TestContext.Current.CancellationToken));

        Assert.NotNull(await _sets.FindAsync(subject, TestContext.Current.CancellationToken));

        _ = await Service.ReportAsync(
            AccessContext.Of(subject),
            generator,
            Source,
            TestContext.Current.CancellationToken);

        _clock.Advance(TimeSpan.FromDays(8));

        _ = await Service.AdvanceAsync(TestContext.Current.CancellationToken);

        Assert.Null(await _sets.FindAsync(subject, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// AUTH-RECOV-007a AC1 and AC2: an invalidation that leaves the account below AAL2
    /// re-reads the password against the single-factor floor, and one that falls short
    /// is marked for change at the next sign-in rather than refused.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_RECOV_007a_AC1_AnInvalidationRereadsThePasswordAgainstTheFloorAsync()
    {
        SubjectId subject = await AccountAsync(Short);
        AuthenticatorId generator = await EnrolledAsync(subject);

        Assert.False(
            (await _passwords.FindAsync(subject, TestContext.Current.CancellationToken))!
                .MeetsSingleFactorFloor);

        _ = await Service.ReportAsync(
            AccessContext.Of(subject),
            generator,
            Source,
            TestContext.Current.CancellationToken);

        _clock.Advance(TimeSpan.FromDays(8));

        _ = await Service.AdvanceAsync(TestContext.Current.CancellationToken);

        Assert.True(
            (await _passwords.FindAsync(subject, TestContext.Current.CancellationToken))!
                .ChangeRequired);
    }

    /// <summary>
    /// AUTH-RECOV-007a AC1: a password that already met the single-factor floor is left
    /// alone by the same invalidation.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_RECOV_007a_AC1_APasswordThatMeetsTheFloorIsNotMarkedAsync()
    {
        SubjectId subject = await AccountAsync();
        AuthenticatorId generator = await EnrolledAsync(subject);

        _ = await Service.ReportAsync(
            AccessContext.Of(subject),
            generator,
            Source,
            TestContext.Current.CancellationToken);

        _clock.Advance(TimeSpan.FromDays(8));

        _ = await Service.AdvanceAsync(TestContext.Current.CancellationToken);

        Assert.False(
            (await _passwords.FindAsync(subject, TestContext.Current.CancellationToken))!
                .ChangeRequired);
    }

    /// <summary>
    /// AUTH-RECOV-008 AC1: an account whose policy holds two credentials reports a loss
    /// through an approver, and the self-service report is not open to it.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_RECOV_008_AC1_SelfServiceLossReportingIsClosedToStaffAsync()
    {
        SubjectId subject = await AccountAsync();
        AuthenticatorId generator = await EnrolledAsync(subject);

        _memberships.Place(subject, Staff);
        _configuration.Set(
            Settings.OrganizationPolicy,
            Staff.ToString(),
            new PolicyOverride(null, null, null, null, SelfServiceRecovery: false, null));

        Assert.Equal(
            ErrorCodes.LossReportNotPermitted,
            Refused(await Service.ReportAsync(
                AccessContext.Of(subject),
                generator,
                Source,
                TestContext.Current.CancellationToken)));

        Assert.Equal(AuthenticatorState.Active, await StateAsync(generator));
    }

    /// <summary>
    /// AUTH-RECOV-007: one report stands per credential, so a second report against the
    /// same one is refused rather than restarting the window.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_RECOV_007_ASecondReportAgainstOneCredentialIsRefusedAsync()
    {
        SubjectId subject = await AccountAsync();
        AuthenticatorId generator = await EnrolledAsync(subject);

        _ = await Service.ReportAsync(
            AccessContext.Of(subject),
            generator,
            Source,
            TestContext.Current.CancellationToken);

        Assert.Equal(
            ErrorCodes.LossReportPending,
            Refused(await Service.ReportAsync(
                AccessContext.Of(subject),
                generator,
                Source,
                TestContext.Current.CancellationToken)));
    }

    /// <summary>
    /// AUTH-RECOV-007 and D-153: the notice repeats across the window, and every one of
    /// them carries the link that ends the report.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_RECOV_007_TheNoticeRepeatsAcrossTheWindowAsync()
    {
        SubjectId subject = await AccountAsync();
        AuthenticatorId generator = await EnrolledAsync(subject);

        _ = await Service.ReportAsync(
            AccessContext.Of(subject),
            generator,
            Source,
            TestContext.Current.CancellationToken);

        string first = _notifications.Mail[^1].Values["token"];
        int sent = _notifications.Mail.Count;

        _clock.Advance(TimeSpan.FromDays(1));

        _ = await Service.AdvanceAsync(TestContext.Current.CancellationToken);

        Assert.True(_notifications.Mail.Count > sent);
        Assert.Equal(first, _notifications.Mail[^1].Values["token"]);
    }

    private LossReports Service =>
        new(
            _reports,
            _authenticators,
            _passwords,
            _sets,
            _identifiers,
            Policies,
            _notifications,
            _credentials,
            _configuration,
            _work,
            _clock,
            _randomness);

    private TotpService Totp =>
        new(_authenticators, _passwords, _configuration, _work, _clock, _randomness);

    private RecoveryCodeService Codes =>
        new(
            _sets,
            new Argon2idHasher(_randomness),
            _configuration,
            _work,
            _clock,
            _randomness);

    private PolicyResolution Policies => new(_memberships, _configuration, _raises);

    private PasswordService Passwords =>
        new(
            _passwords,
            new PasswordScreening(_corpus, _words, _configuration, _screening),
            new Argon2idHasher(_randomness),
            _configuration,
            _work,
            _clock);

    private string Code(AuthenticatorId generator) =>
        new Totp(
                _authenticators.All
                    .Single(credential => credential.Id == generator)
                    .Totp!.Secret.ToArray(),
                TotpCodes.StepSeconds,
                OtpHashMode.Sha1,
                TotpCodes.Digits)
            .ComputeTotp(_clock.GetUtcNow().UtcDateTime);

    private async ValueTask<AuthenticatorState> StateAsync(AuthenticatorId credential) =>
        (await _authenticators.FindAsync(credential, TestContext.Current.CancellationToken))!.State;

    private async ValueTask<IReadOnlySet<Factor>> UsableAsync(SubjectId subject) =>
        HeldFactors.Of(
                await _authenticators.OfAsync(subject, TestContext.Current.CancellationToken),
                password: true)
            .Usable;

    private async ValueTask<AssuranceLevel> ReachableAsync(SubjectId subject) =>
        StepUp.Reachable(
                HeldFactors.Of(
                        await _authenticators.OfAsync(subject, TestContext.Current.CancellationToken),
                        password: true)
                    .Standing)
            .Level;

    private async ValueTask<AuthenticatorId> EnrolledAsync(SubjectId subject)
    {
        TotpEnrolment enrolment = Value(await Totp.BeginAsync(
            subject,
            Label(),
            TestContext.Current.CancellationToken))!;

        _ = await Totp.ConfirmAsync(
            subject,
            enrolment.Id,
            new Totp(
                    enrolment.Secret.ToArray(),
                    TotpCodes.StepSeconds,
                    OtpHashMode.Sha1,
                    TotpCodes.Digits)
                .ComputeTotp(_clock.GetUtcNow().UtcDateTime),
            TestContext.Current.CancellationToken);

        return enrolment.Id;
    }

    private static CredentialLabel Label() =>
        CredentialLabel.TryParse("Phone", out CredentialLabel label)
            ? label
            : throw new InvalidOperationException("The label is not one.");

    private async ValueTask<SubjectId> AccountAsync(string password = Secret)
    {
        var subject = new SubjectId(Guid.NewGuid());

        _accounts.Stands(subject, AccountState.Active);
        _accounts.Registered(subject, _clock.GetUtcNow());
        _identifiers.Reads(subject, Language);

        IdentifierId email = _identifiers.Verified(subject, IdentifierKind.Email, Address);

        _ = _identifiers.Verified(subject, IdentifierKind.Phone, Number);

        await _identifiers.PromoteAsync(subject, email, TestContext.Current.CancellationToken);

        byte[] presented = Encoding.UTF8.GetBytes(password);

        _ = await Passwords.SetAsync(
            subject,
            presented,
            [],
            AssuranceLevel.Aal2,
            TestContext.Current.CancellationToken);

        await _work.CommitAsync(TestContext.Current.CancellationToken);

        return subject;
    }

    private static bool Succeeded(Result result) => result.Match(() => true, _ => false);

    private static ErrorCode Refused<TValue>(Result<TValue> result) =>
        result.Match(_ => default, error => error.Code);

    private static TValue? Value<TValue>(Result<TValue> result)
        where TValue : class =>
        result.Match<TValue?>(value => value, _ => null);
}
