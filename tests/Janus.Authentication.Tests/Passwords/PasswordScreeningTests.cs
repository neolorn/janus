using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Janus.Authentication.Passwords;
using Janus.Core;
using Janus.Core.Configuration;
using Xunit;

namespace Janus.Authentication.Tests.Passwords;

/// <summary>
/// Screening at set and at change (AUTH-PASS-004, INT-PWD-001 to 003).
/// </summary>
[Trait("kind", "unit")]
public sealed class PasswordScreeningTests
{
    private const string Breached = "correct horse battery staple";

    private readonly LeakedPasswordCorpusInMemory _corpus = new();
    private readonly WordListInMemory _words = new();
    private readonly ConfigurationInMemory _configuration = new();
    private readonly ScreeningLogInMemory _log = new();

    private PasswordScreening Screening =>
        new(_corpus, _words, _configuration, _log);

    /// <summary>
    /// AUTH-PASS-004 AC1: a password in the corpus is refused however long it is.
    /// </summary>
    [Fact]
    public async Task AUTH_PASS_004_AC1_AKnownBreachedPasswordIsRefusedWhateverItsLengthAsync()
    {
        _corpus.Hold(BlocklistSource.RangeApi, Breached);

        Result screened = await Screening.ScreenAsync(
            Encoding.UTF8.GetBytes(Breached),
            [],
            TestContext.Current.CancellationToken);

        Assert.Equal(ErrorCodes.PasswordBlocklisted, Refusal(screened));
    }

    /// <summary>
    /// INT-PWD-001 AC1, AC2: only the prefix leaves the deployment, and the full hash
    /// never does.
    /// </summary>
    [Fact]
    public async Task INT_PWD_001_AC1_OnlyAPrefixLeavesTheDeploymentAsync()
    {
        _corpus.Hold(BlocklistSource.RangeApi, Breached);

        Result unused = await Screening.ScreenAsync(
            Encoding.UTF8.GetBytes(Breached),
            [],
            TestContext.Current.CancellationToken);

        Assert.Equal(ErrorCodes.PasswordBlocklisted, Refusal(unused));
        Assert.All(_corpus.Asked, asked => Assert.Equal(5, asked.Length));
        Assert.DoesNotContain(LeakedPasswordCorpusInMemory.Hash(Breached), _corpus.Asked);
    }

    /// <summary>
    /// AUTH-PASS-004 AC2 and INT-PWD-002 AC1: with the configured corpus unreachable
    /// the offline one runs and the fall back is recorded.
    /// </summary>
    [Fact]
    public async Task INT_PWD_002_AC1_WithTheServiceUnreachableTheFallbackRunsAndIsRecordedAsync()
    {
        _corpus.Unreachable.Add(BlocklistSource.RangeApi);
        _corpus.Hold(BlocklistSource.Offline, Breached);

        Result screened = await Screening.ScreenAsync(
            Encoding.UTF8.GetBytes(Breached),
            [],
            TestContext.Current.CancellationToken);

        Assert.Equal(ErrorCodes.PasswordBlocklisted, Refusal(screened));
        Assert.Equal(
            [(BlocklistSource.RangeApi, BlocklistSource.Offline)],
            _log.Degradations);
    }

    /// <summary>
    /// AUTH-PASS-004 AC3 and INT-PWD-002 AC2: with neither corpus able to answer the
    /// operation fails rather than accepting a password nothing screened.
    /// </summary>
    [Fact]
    public async Task INT_PWD_002_AC2_WithBothUnavailableTheOperationFailsAsync()
    {
        _corpus.Unreachable.Add(BlocklistSource.RangeApi);
        _corpus.Unreachable.Add(BlocklistSource.Offline);

        Result screened = await Screening.ScreenAsync(
            Encoding.UTF8.GetBytes("a password nothing holds"),
            [],
            TestContext.Current.CancellationToken);

        Assert.Equal(ErrorCodes.ScreeningUnavailable, Refusal(screened));
    }

    /// <summary>
    /// INT-PWD-003 AC1: the deployment reads its corpus from somewhere else by naming
    /// a value, and nothing else changes.
    /// </summary>
    [Fact]
    public async Task INT_PWD_003_AC1_SwitchingToTheSelfHostedCorpusIsAConfigurationChangeAsync()
    {
        _configuration.Set(Settings.PasswordBlocklistSource, BlocklistSource.SelfHosted);
        _corpus.Hold(BlocklistSource.SelfHosted, Breached);

        Result screened = await Screening.ScreenAsync(
            Encoding.UTF8.GetBytes(Breached),
            [],
            TestContext.Current.CancellationToken);

        Assert.Equal(ErrorCodes.PasswordBlocklisted, Refusal(screened));
        Assert.Empty(_log.Degradations);
    }

    /// <summary>
    /// AUTH-PASS-004 AC4: at the shipped sources a password holding the person's own
    /// word and a listed word, absent from the corpus, is accepted.
    /// </summary>
    [Fact]
    public async Task AUTH_PASS_004_AC4_AtTheDefaultSourcesAnOwnWordDoesNotRefuseAsync()
    {
        _words.Words.Add("summer");

        Result screened = await Screening.ScreenAsync(
            Encoding.UTF8.GetBytes("hossam summer morning"),
            ["hossam"],
            TestContext.Current.CancellationToken);

        Assert.Null(Refusal(screened));
    }

    /// <summary>
    /// AUTH-PASS-004 AC5: with the context source enabled the same password is
    /// refused.
    /// </summary>
    [Fact]
    public async Task AUTH_PASS_004_AC5_WithContextEnabledTheOwnWordRefusesAsync()
    {
        _configuration.Set(
            Settings.PasswordBlocklistSources,
            new HashSet<BlocklistRejectionSource>
            {
                BlocklistRejectionSource.Leaked,
                BlocklistRejectionSource.Context,
            });

        Result screened = await Screening.ScreenAsync(
            Encoding.UTF8.GetBytes("hossam summer morning"),
            ["hossam"],
            TestContext.Current.CancellationToken);

        Assert.Equal(ErrorCodes.PasswordBlocklisted, Refusal(screened));
    }

    /// <summary>
    /// AUTH-PASS-004: with the dictionary source enabled a listed word refuses, and a
    /// list that cannot be read refuses the operation rather than skipping it.
    /// </summary>
    [Fact]
    public async Task ScreenAsync_DictionaryEnabledAndUnreadable_RefusesRatherThanSkipsAsync()
    {
        _configuration.Set(
            Settings.PasswordBlocklistSources,
            new HashSet<BlocklistRejectionSource>
            {
                BlocklistRejectionSource.Leaked,
                BlocklistRejectionSource.Dictionary,
            });
        _words.Unreachable = true;

        Result screened = await Screening.ScreenAsync(
            Encoding.UTF8.GetBytes("a password nothing holds"),
            [],
            TestContext.Current.CancellationToken);

        Assert.Equal(ErrorCodes.ScreeningUnavailable, Refusal(screened));
    }

    private static ErrorCode? Refusal(Result screened) =>
        screened.Match<ErrorCode?>(() => null, error => error.Code);
}
