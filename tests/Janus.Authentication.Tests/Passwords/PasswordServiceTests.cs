using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Janus.Authentication.Passwords;
using Janus.Core;
using Janus.Core.Configuration;
using Xunit;

namespace Janus.Authentication.Tests.Passwords;

/// <summary>
/// Setting a password and presenting one, with the floor, the screening, the hashing
/// and the rehash in one path (AUTH-PASS-001a, AUTH-PASS-004, AUTH-PASS-007).
/// </summary>
[Trait("kind", "unit")]
public sealed class PasswordServiceTests : IAsyncDisposable
{
    private const string Chosen = "orangemarmaladeandtoast";
    private const string Short = "tenletters";

    private static readonly DateTimeOffset Noon =
        new(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly PasswordStoreInMemory _passwords = new();
    private readonly LeakedPasswordCorpusInMemory _corpus = new();
    private readonly WordListInMemory _words = new();
    private readonly ConfigurationInMemory _configuration = new();
    private readonly ScreeningLogInMemory _log = new();
    private readonly EventsInMemory _events = new();
    private readonly UnitOfWorkInMemory _work = new();
    private readonly FixedClock _clock = new(Noon);
    private readonly RandomNumberGenerator _randomness = RandomNumberGenerator.Create();

    private PasswordService Service => new(
        _passwords,
        new PasswordScreening(_corpus, _words, _configuration, _log, _events, _clock),
        new Argon2idHasher(_randomness),
        _configuration,
        _work,
        _clock);

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await _work.DisposeAsync();
        _randomness.Dispose();
    }

    /// <summary>
    /// AUTH-PASS-001a AC3: the flag is recorded beside the hash, and a change from a
    /// password that stands alone to one that does not carries it down.
    /// </summary>
    [Fact]
    public async Task AUTH_PASS_001a_AC3_TheFloorFlagIsRecordedAndFollowsEveryChangeAsync()
    {
        SubjectId subject = Subject();

        await SetAsync(subject, Chosen, AssuranceLevel.Aal1);

        Assert.True((await HeldAsync(subject)).MeetsSingleFactorFloor);

        await SetAsync(subject, Short, AssuranceLevel.Aal2);

        Assert.False((await HeldAsync(subject)).MeetsSingleFactorFloor);
    }

    /// <summary>
    /// AUTH-PASS-001a AC2: a password below the single-factor floor is refused on an
    /// account that reaches AAL1 and recorded on one that reaches AAL2.
    /// </summary>
    [Fact]
    public async Task AUTH_PASS_001a_AC2_TheShorterFloorIsReachedByHoldingASecondFactorAsync()
    {
        SubjectId alone = Subject();

        Assert.Equal(
            ErrorCodes.PasswordTooShort,
            Refusal(await Service.SetAsync(
                alone,
                Encoding.UTF8.GetBytes(Short),
                [],
                AssuranceLevel.Aal1,
                TestContext.Current.CancellationToken)));

        Assert.Null(await _passwords.FindAsync(alone, TestContext.Current.CancellationToken));

        SubjectId withSecond = Subject();

        await SetAsync(withSecond, Short, AssuranceLevel.Aal2);

        Assert.NotNull(await _passwords.FindAsync(withSecond, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// AUTH-PASS-004 AC3: a password that could not be screened is not recorded.
    /// </summary>
    [Fact]
    public async Task AUTH_PASS_004_AC3_APasswordThatCouldNotBeScreenedIsNotRecordedAsync()
    {
        SubjectId subject = Subject();

        _corpus.Unreachable.Add(BlocklistSource.RangeApi);
        _corpus.Unreachable.Add(BlocklistSource.Offline);

        Assert.Equal(
            ErrorCodes.ScreeningUnavailable,
            Refusal(await Service.SetAsync(
                subject,
                Encoding.UTF8.GetBytes(Chosen),
                [],
                AssuranceLevel.Aal1,
                TestContext.Current.CancellationToken)));

        Assert.Null(await _passwords.FindAsync(subject, TestContext.Current.CancellationToken));
        Assert.Equal(0, _work.Committed);
    }

    /// <summary>
    /// AUTH-PASS-007 AC1, AC2: the password set at one strength still verifies after
    /// the parameters are raised, and the sign-in that proved it carries the hash
    /// onto the new ones without the person being told.
    /// </summary>
    [Fact]
    public async Task AUTH_PASS_007_AC2_ASignInAfterARaiseUpgradesTheHashAsync()
    {
        SubjectId subject = Subject();

        await SetAsync(subject, Chosen, AssuranceLevel.Aal1);

        Argon2StrengthClass before = (await HeldAsync(subject)).Hash.Parameters;

        _configuration.Set(Settings.PasswordArgon2Memory, 47104);
        _configuration.Set(Settings.PasswordArgon2Iterations, 1);

        PasswordVerification verified = await VerifiedAsync(subject, Chosen);

        Assert.False(verified.ChangeRequired);
        Assert.Equal(1, _passwords.Rehashed);
        Assert.NotEqual(before, (await HeldAsync(subject)).Hash.Parameters);
        Assert.Equal(new Argon2StrengthClass(47104, 1), (await HeldAsync(subject)).Hash.Parameters);

        await VerifiedAsync(subject, Chosen);

        Assert.Equal(1, _passwords.Rehashed);
    }

    /// <summary>
    /// AUTH-PASS-007 AC1: the parameters left where they stand rehash nothing.
    /// </summary>
    [Fact]
    public async Task AUTH_PASS_007_AC1_AnUnchangedDeploymentRehashesNothingAsync()
    {
        SubjectId subject = Subject();

        await SetAsync(subject, Chosen, AssuranceLevel.Aal1);
        await VerifiedAsync(subject, Chosen);

        Assert.Equal(0, _passwords.Rehashed);
    }

    /// <summary>
    /// AUTH-PASS-004 AC5: a profile field set to the password after the password was
    /// set produces a prompt to change at the next sign-in, and the sign-in
    /// completes.
    /// </summary>
    [Fact]
    public async Task AUTH_PASS_004_AC5_AProfileFieldMatchingThePasswordPromptsAtTheNextSignInAsync()
    {
        SubjectId subject = Subject();

        await SetAsync(subject, Chosen, AssuranceLevel.Aal1);

        _configuration.Set<IReadOnlySet<BlocklistRejectionSource>>(
            Settings.PasswordBlocklistSources,
            new HashSet<BlocklistRejectionSource> { BlocklistRejectionSource.Context });

        PasswordVerification prompted = await VerifiedAsync(subject, Chosen, [Chosen]);

        Assert.True(prompted.ChangeRequired);
    }

    /// <summary>
    /// AUTH-PASS-007: a password that is not the one the account holds is refused,
    /// and so is a presentation to an account holding none.
    /// </summary>
    [Fact]
    public async Task VerifyAsync_ADifferentPasswordOrNoneAtAll_IsRefusedAsync()
    {
        SubjectId subject = Subject();

        Assert.Equal(ErrorCodes.FactorRejected, Refusal(await Service.VerifyAsync(
            subject,
            Encoding.UTF8.GetBytes(Chosen),
            [],
            TestContext.Current.CancellationToken)));

        await SetAsync(subject, Chosen, AssuranceLevel.Aal1);

        Assert.Equal(ErrorCodes.FactorRejected, Refusal(await Service.VerifyAsync(
            subject,
            Encoding.UTF8.GetBytes("somethingelseentirely"),
            [],
            TestContext.Current.CancellationToken)));
    }

    private SubjectId Subject() => SubjectId.New(_randomness);

    private static ErrorCode Refusal<TValue>(Result<TValue> result) =>
        result.Match(_ => throw new Xunit.Sdk.XunitException("The operation succeeded."), error => error.Code);

    private async Task SetAsync(SubjectId subject, string password, AssuranceLevel reachable)
    {
        Result<PasswordFeedback> set = await Service.SetAsync(
            subject,
            Encoding.UTF8.GetBytes(password),
            [],
            reachable,
            TestContext.Current.CancellationToken);

        set.Switch(
            _ => { },
            error => throw new Xunit.Sdk.XunitException($"The password was refused: {error.Code}."));
    }

    private async Task<Password> HeldAsync(SubjectId subject) =>
        await _passwords.FindAsync(subject, TestContext.Current.CancellationToken)
        ?? throw new Xunit.Sdk.XunitException("The account holds no password.");

    private async Task<PasswordVerification> VerifiedAsync(
        SubjectId subject,
        string password,
        IReadOnlyCollection<string>? ownWords = null)
    {
        Result<PasswordVerification> verified = await Service.VerifyAsync(
            subject,
            Encoding.UTF8.GetBytes(password),
            ownWords ?? [],
            TestContext.Current.CancellationToken);

        return verified.Match(
            value => value,
            error => throw new Xunit.Sdk.XunitException($"The password was refused: {error.Code}."));
    }
}
