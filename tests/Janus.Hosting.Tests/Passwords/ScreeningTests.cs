using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Janus.Authentication.Passwords;
using Janus.Core;
using Janus.Core.Configuration;
using Janus.Hosting.Passwords;
using Janus.Hosting.Tests.Authorization;
using Xunit;

namespace Janus.Hosting.Tests.Passwords;

/// <summary>
/// Compromised-password screening as the deployment runs it: a prefix out, a range
/// back, a fallback that is loud, and nothing accepted unscreened
/// (AUTH-PASS-004, INT-PWD-001, INT-PWD-002, INT-PWD-003).
/// </summary>
[Trait("kind", "unit")]
public sealed class ScreeningTests : IDisposable
{
    private const string Password = "a horse outstanding in its field";
    private const string Line = "\n";

    private static readonly DateTimeOffset Noon = new(2026, 9, 19, 12, 0, 0, TimeSpan.Zero);

    private readonly string _directory = Path.Combine(
        AppContext.BaseDirectory,
        "corpus-" + Guid.NewGuid().ToString("n"));

    private readonly RangeApiInMemory _service = new();
    private readonly ConfigurationInMemory _configuration = new();
    private readonly ScreeningLogInMemory _log = new();

    /// <summary>
    /// INT-PWD-001 AC1: the request carries the first five characters of the hash and
    /// nothing else, so the service is asked about a range and never about a password.
    /// </summary>
    [Fact]
    public async Task INT_PWD_001_AC1_TheRequestCarriesThePrefixOnlyAsync()
    {
        _service.Holds(Prefix(), Suffix() + ":12");

        Assert.False(await AcceptedAsync());

        Uri asked = Assert.Single(_service.Asked);

        Assert.Equal(Prefix(), asked.Segments[^1]);
        Assert.EndsWith("/range/" + Prefix(), asked.AbsolutePath, StringComparison.Ordinal);
    }

    /// <summary>
    /// INT-PWD-001 AC2: no full hash appears anywhere in what goes out, which is what
    /// keeps the password unguessable to the service.
    /// </summary>
    [Fact]
    public async Task INT_PWD_001_AC2_NoFullHashAppearsInTheRequestAsync()
    {
        _service.Holds(Prefix(), Suffix());

        Assert.False(await AcceptedAsync());

        Uri asked = Assert.Single(_service.Asked);

        Assert.DoesNotContain(Hash(), asked.AbsoluteUri, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(Suffix(), asked.AbsoluteUri, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(Password, asked.AbsoluteUri, StringComparison.Ordinal);
    }

    /// <summary>
    /// INT-PWD-002 AC1 and AUTH-PASS-004 AC2: with the service unreachable the offline
    /// list answers and the fall back is recorded, so a degradation is visible rather
    /// than silent.
    /// </summary>
    [Fact]
    public async Task INT_PWD_002_AC1_WithTheServiceUnreachableTheOfflineListAnswersAsync()
    {
        _service.Reachable = false;
        await CorpusAsync(LeakedPasswordCorpus.OfflineFile, Hash());

        Assert.False(await AcceptedAsync());
        Assert.Equal(
            [(BlocklistSource.RangeApi, BlocklistSource.Offline)],
            [.. _log.Entries]);
    }

    /// <summary>
    /// AUTH-PASS-004 AC2: the fall back screens rather than waves through, so a
    /// password the offline list does not hold is accepted on that list's word.
    /// </summary>
    [Fact]
    public async Task AUTH_PASS_004_AC2_TheOfflineListAcceptsWhatItDoesNotHoldAsync()
    {
        _service.Reachable = false;
        await CorpusAsync(LeakedPasswordCorpus.OfflineFile, "0000000000000000000000000000000000000000");

        Assert.True(await AcceptedAsync());
        Assert.Single(_log.Entries);
    }

    /// <summary>
    /// INT-PWD-002 AC2: with the service unreachable and no offline list to fall back
    /// to, the password is refused rather than accepted unscreened.
    /// </summary>
    [Fact]
    public async Task INT_PWD_002_AC2_WithNeitherAvailableTheOperationFailsAsync()
    {
        _service.Reachable = false;

        Assert.Equal(ErrorCodes.ScreeningUnavailable, await RefusalAsync());
    }

    /// <summary>
    /// INT-PWD-003 AC1: naming the self-hosted corpus is the whole switch, and the
    /// corpus the deployment holds is what answers afterwards.
    /// </summary>
    [Fact]
    public async Task INT_PWD_003_AC1_SwitchingToTheSelfHostedCorpusIsConfigurationOnlyAsync()
    {
        await CorpusAsync(LeakedPasswordCorpus.SelfHostedFile, Hash());
        _configuration.Set(Settings.PasswordBlocklistSource, BlocklistSource.SelfHosted);

        Assert.False(await AcceptedAsync());
        Assert.Empty(_service.Asked);
        Assert.Empty(_log.Entries);
    }

    /// <summary>
    /// A corpus older than the deployment admits cannot be screened on, so it answers
    /// nothing and the password is refused rather than judged against stale data.
    /// </summary>
    [Fact]
    public async Task ScreenAsync_ACorpusOlderThanTheMaximumAge_RefusesAsync()
    {
        _service.Reachable = false;
        await CorpusAsync(LeakedPasswordCorpus.OfflineFile, Hash(), drawn: "2026-01-01");

        Assert.Equal(ErrorCodes.ScreeningUnavailable, await RefusalAsync());
    }

    /// <summary>
    /// A corpus with no date cannot be judged against the maximum age at all, so it is
    /// treated as no corpus.
    /// </summary>
    [Fact]
    public async Task ScreenAsync_ACorpusWithNoDate_RefusesAsync()
    {
        _service.Reachable = false;
        await WrittenAsync(LeakedPasswordCorpus.OfflineFile, Hash() + Line);

        Assert.Equal(ErrorCodes.ScreeningUnavailable, await RefusalAsync());
    }

    /// <summary>
    /// AUTH-PASS-004: a deployment that rejects on a word list and holds none refuses
    /// the password, because a source that cannot answer never answers yes.
    /// </summary>
    [Fact]
    public async Task ScreenAsync_TheDictionarySourceWithNoList_RefusesAsync()
    {
        _service.Holds(Prefix(), "0000000000000000000000000000000000000");
        _configuration.Set<IReadOnlySet<BlocklistRejectionSource>>(
            Settings.PasswordBlocklistSources,
            new HashSet<BlocklistRejectionSource>
            {
                BlocklistRejectionSource.Leaked,
                BlocklistRejectionSource.Dictionary,
            });

        Assert.Equal(ErrorCodes.ScreeningUnavailable, await RefusalAsync());
    }

    /// <summary>
    /// AUTH-PASS-004: a word the list holds is a word the password is refused for,
    /// where the deployment rejects on the list.
    /// </summary>
    [Fact]
    public async Task ScreenAsync_TheDictionarySourceAndAListedWord_RefusesAsync()
    {
        _service.Holds(Prefix(), "0000000000000000000000000000000000000");
        await WrittenAsync(WordList.WordsFile, "# an English list" + Line + "HORSE" + Line + "FIELD" + Line);
        _configuration.Set<IReadOnlySet<BlocklistRejectionSource>>(
            Settings.PasswordBlocklistSources,
            new HashSet<BlocklistRejectionSource>
            {
                BlocklistRejectionSource.Leaked,
                BlocklistRejectionSource.Dictionary,
            });

        Assert.Equal(ErrorCodes.PasswordBlocklisted, await RefusalAsync());
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _service.Dispose();

        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    // The corpus is published as ranges of this hash (INT-PWD-001), so the test
    // computes the same lookup key the screening does; it is no security claim.
#pragma warning disable CA5350 // The corpus is published as SHA-1 ranges (INT-PWD-001); the value is a lookup key.
    private static string Hash() =>
        Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes(Password)));
#pragma warning restore CA5350

    private static string Prefix() => Hash()[..5];

    private static string Suffix() => Hash()[5..];

    private async Task<bool> AcceptedAsync() =>
        (await ScreenedAsync()).Match(() => true, _ => false);

    private async Task<ErrorCode> RefusalAsync() => (await ScreenedAsync()).Match(
        () => throw new Xunit.Sdk.XunitException("The password was accepted."),
        error => error.Code);

    private async Task<Result> ScreenedAsync()
    {
        using var client = new HttpClient(_service, disposeHandler: false)
        {
            BaseAddress = LeakedPasswordCorpus.Provider,
        };

        var screening = new PasswordScreening(
            new LeakedPasswordCorpus(
                client,
                _configuration,
                new FixedTime(Noon),
                _directory),
            new WordList(_directory),
            _configuration,
            _log);

        return await screening.ScreenAsync(
            Encoding.UTF8.GetBytes(Password),
            [],
            TestContext.Current.CancellationToken);
    }

    private async Task CorpusAsync(string file, string hash, string drawn = "2026-09-18") =>
        await WrittenAsync(file, "# " + drawn + Line + hash + ":7" + Line);

    private async Task WrittenAsync(string file, string contents)
    {
        _ = Directory.CreateDirectory(_directory);

        await File.WriteAllTextAsync(
            Path.Combine(_directory, file),
            contents,
            TestContext.Current.CancellationToken);
    }
}
