using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Janus.Authentication.Factors;
using Janus.Authentication.Passwords;
using Janus.Core;
using Janus.Core.Configuration;
using Xunit;

namespace Janus.Authentication.Tests.Factors;

/// <summary>
/// Recovery codes: how they are issued, spent, replaced and reminded about
/// (AUTH-FACT-008, AUTH-FACT-009).
/// </summary>
[Trait("kind", "unit")]
public sealed class RecoveryCodeServiceTests : IAsyncDisposable
{
    private static readonly DateTimeOffset Noon =
        new(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly RecoveryCodeStoreInMemory _sets = new();
    private readonly ConfigurationInMemory _configuration = new();
    private readonly UnitOfWorkInMemory _work = new();
    private readonly FixedClock _clock = new(Noon);
    private readonly RandomNumberGenerator _randomness = RandomNumberGenerator.Create();

    private RecoveryCodeService Service =>
        new(_sets, new Argon2idHasher(_randomness), _configuration, _work, _clock, _randomness);

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await _work.DisposeAsync();
        _randomness.Dispose();
    }

    /// <summary>
    /// AUTH-FACT-008: codes are issued in sets of ten, each ten symbols of Crockford
    /// base32 shown as two groups of five.
    /// </summary>
    [Fact]
    public async Task AUTH_FACT_008_CodesAreIssuedInSetsOfTenAsync()
    {
        IReadOnlyList<string> codes = await GeneratedAsync(Subject());

        Assert.Equal(10, codes.Count);
        Assert.Equal(10, codes.Distinct(StringComparer.Ordinal).Count());

        foreach (string code in codes)
        {
            Assert.Equal(RecoveryCode.Symbols, RecoveryCode.Canonical(code).Length);
            Assert.Equal(RecoveryCode.Symbols + 1, code.Length);
            Assert.Equal('-', code[RecoveryCode.GroupSize]);
        }
    }

    /// <summary>
    /// AUTH-FACT-008 AC1: a used code is rejected on second presentation.
    /// </summary>
    [Fact]
    public async Task AUTH_FACT_008_AC1_AUsedCodeIsRejectedOnSecondPresentationAsync()
    {
        SubjectId subject = Subject();
        IReadOnlyList<string> codes = await GeneratedAsync(subject);

        Assert.Null(Refusal(await Service.SpendAsync(
            subject,
            codes[0],
            TestContext.Current.CancellationToken)));
        Assert.Equal(
            ErrorCodes.CodeInvalid,
            Refusal(await Service.SpendAsync(
                subject,
                codes[0],
                TestContext.Current.CancellationToken)));
    }

    /// <summary>
    /// AUTH-FACT-008 AC3: a dump of the table does not yield usable codes, the set
    /// holding hashes and no code.
    /// </summary>
    [Fact]
    public async Task AUTH_FACT_008_AC3_ADumpOfTheTableYieldsNoUsableCodeAsync()
    {
        SubjectId subject = Subject();
        IReadOnlyList<string> codes = await GeneratedAsync(subject);
        RecoveryCodeSet set = (await _sets.FindAsync(subject, TestContext.Current.CancellationToken))!;

        foreach (RecoveryCodeEntry entry in set.Codes)
        {
            string stored = entry.Hash.Encoded;

            Assert.StartsWith("$argon2id$", stored, StringComparison.Ordinal);
            Assert.DoesNotContain(
                codes,
                code => stored.Contains(RecoveryCode.Canonical(code), StringComparison.OrdinalIgnoreCase));
        }
    }

    /// <summary>
    /// AUTH-FACT-008 AC4: the set records when the codes were shown and when they
    /// were exported, and the export stays unset otherwise.
    /// </summary>
    [Fact]
    public async Task AUTH_FACT_008_AC4_TheSetRecordsWhenItWasShownAndExportedAsync()
    {
        SubjectId subject = Subject();
        await GeneratedAsync(subject);

        await Service.ShownAsync(subject, exported: false, TestContext.Current.CancellationToken);

        RecoveryCodeSet shown = (await _sets.FindAsync(subject, TestContext.Current.CancellationToken))!;

        Assert.Equal(Noon, shown.ViewedAt);
        Assert.Null(shown.ExportedAt);

        _clock.Advance(TimeSpan.FromMinutes(1));
        await Service.ShownAsync(subject, exported: true, TestContext.Current.CancellationToken);

        RecoveryCodeSet exported = (await _sets.FindAsync(subject, TestContext.Current.CancellationToken))!;

        Assert.Equal(Noon, exported.ViewedAt);
        Assert.Equal(Noon + TimeSpan.FromMinutes(1), exported.ExportedAt);
    }

    /// <summary>
    /// AUTH-RECOV-006 AC2: copy, download and print are three names for one thing the
    /// library is told about, and each of them sets the export against the set; the
    /// first one stands, so a second does not move it.
    /// </summary>
    [Fact]
    public async Task AUTH_RECOV_006_AC2_EachWayOfTakingTheCodesAwaySetsTheExportAsync()
    {
        SubjectId subject = Subject();
        await GeneratedAsync(subject);

        await Service.ShownAsync(subject, exported: true, TestContext.Current.CancellationToken);

        Assert.Equal(Noon, (await SetAsync(subject)).ExportedAt);

        _clock.Advance(TimeSpan.FromMinutes(1));
        await Service.ShownAsync(subject, exported: true, TestContext.Current.CancellationToken);

        Assert.Equal(Noon, (await SetAsync(subject)).ExportedAt);
    }

    /// <summary>
    /// AUTH-FACT-008 AC5: a set older than the reminder age produces one reminder and
    /// no further reminder until the set is regenerated.
    /// </summary>
    [Fact]
    public async Task AUTH_FACT_008_AC5_AnOldSetRemindsItsOwnerOnceAsync()
    {
        SubjectId subject = Subject();
        await GeneratedAsync(subject);

        Assert.False(Value(await Service.RemindAsync(subject, TestContext.Current.CancellationToken)));

        _clock.Advance(TimeSpan.FromDays(365));

        Assert.True(Value(await Service.RemindAsync(subject, TestContext.Current.CancellationToken)));
        Assert.False(Value(await Service.RemindAsync(subject, TestContext.Current.CancellationToken)));

        await GeneratedAsync(subject);
        _clock.Advance(TimeSpan.FromDays(365));

        Assert.True(Value(await Service.RemindAsync(subject, TestContext.Current.CancellationToken)));
    }

    /// <summary>
    /// AUTH-FACT-009 AC1: after regeneration, no code from the previous set
    /// validates.
    /// </summary>
    [Fact]
    public async Task AUTH_FACT_009_AC1_NoCodeOfThePreviousSetValidatesAsync()
    {
        SubjectId subject = Subject();
        IReadOnlyList<string> first = await GeneratedAsync(subject);
        IReadOnlyList<string> second = await GeneratedAsync(subject);

        foreach (string code in first)
        {
            Assert.Equal(
                ErrorCodes.CodeInvalid,
                Refusal(await Service.SpendAsync(
                    subject,
                    code,
                    TestContext.Current.CancellationToken)));
        }

        Assert.Null(Refusal(await Service.SpendAsync(
            subject,
            second[0],
            TestContext.Current.CancellationToken)));
    }

    /// <summary>
    /// AUTH-FACT-009 AC2: the remaining unused count is what the account displays.
    /// </summary>
    [Fact]
    public async Task AUTH_FACT_009_AC2_TheRemainingCountIsVisibleAsync()
    {
        SubjectId subject = Subject();
        IReadOnlyList<string> codes = await GeneratedAsync(subject);

        Assert.Equal(10, Value(await Service.RemainingAsync(subject, TestContext.Current.CancellationToken)));

        await Service.SpendAsync(subject, codes[3], TestContext.Current.CancellationToken);
        await Service.SpendAsync(subject, codes[7], TestContext.Current.CancellationToken);

        Assert.Equal(8, Value(await Service.RemainingAsync(subject, TestContext.Current.CancellationToken)));
    }

    /// <summary>
    /// AUTH-FACT-008: a code read off a sheet of paper is accepted whatever the
    /// reader made of the symbols the alphabet leaves out.
    /// </summary>
    [Fact]
    public async Task AUTH_FACT_008_ACodeReadBackByHandIsAcceptedAsync()
    {
        SubjectId subject = Subject();
        IReadOnlyList<string> codes = await GeneratedAsync(subject);
        string typed = string.Concat(codes[0].Select(char.ToLowerInvariant))
            .Replace("-", " ", StringComparison.Ordinal)
            .Replace("0", "o", StringComparison.Ordinal)
            .Replace("1", "l", StringComparison.Ordinal);

        Assert.Null(Refusal(await Service.SpendAsync(
            subject,
            typed,
            TestContext.Current.CancellationToken)));
    }

    /// <summary>
    /// AUTH-FACT-008: how many codes a set holds is what the deployment configures.
    /// </summary>
    [Fact]
    public async Task AUTH_FACT_008_TheCountIsWhatTheDeploymentConfiguresAsync()
    {
        _configuration.Set(Settings.FactorRecoveryCodesCount, 6);

        Assert.Equal(6, (await GeneratedAsync(Subject())).Count);
    }

    /// <summary>
    /// AUTH-FACT-008: an account holding no set spends nothing.
    /// </summary>
    [Fact]
    public async Task SpendAsync_AnAccountHoldingNoSet_IsRefusedAsync() =>
        Assert.Equal(
            ErrorCodes.CodeInvalid,
            Refusal(await Service.SpendAsync(
                Subject(),
                "ABCDE-FGHJK",
                TestContext.Current.CancellationToken)));

    private static TValue Value<TValue>(Result<TValue> result) =>
        result.Match(value => value, error => throw new Xunit.Sdk.XunitException(error.Code.ToString()));

    private static ErrorCode? Refusal(Result result) =>
        result.Match<ErrorCode?>(() => null, error => error.Code);

    private SubjectId Subject() => SubjectId.New(_randomness);

    private async ValueTask<RecoveryCodeSet> SetAsync(SubjectId subject) =>
        (await _sets.FindAsync(subject, TestContext.Current.CancellationToken))!;


    private async ValueTask<IReadOnlyList<string>> GeneratedAsync(SubjectId subject)
    {
        // The hashing parameters a test runs at are the cheapest the catalogue
        // permits, the cost of the real ones being the point of them.
        _configuration.Set(Settings.PasswordArgon2Memory, 7168);
        _configuration.Set(Settings.PasswordArgon2Iterations, 5);

        return Value(await Service.GenerateAsync(subject, TestContext.Current.CancellationToken));
    }
}
