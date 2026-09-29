using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Janus.Authentication.Alerting;
using Janus.Authentication.Passwords;
using Janus.Authentication.Tests;
using Janus.Authentication.Tests.Sending;
using Janus.Core;
using Janus.Core.Configuration;
using Janus.Hosting.Bff;
using Janus.Hosting.Passwords;
using Janus.Hosting.Tests.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
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
    private const string Listed = "password";
    private const string SelfHosted = "https://corpus.example/range";
    private const string Line = "\n";
    private const string Connection = "Host=nowhere;Database=identity";

    private static readonly DateTimeOffset Noon = new(2026, 9, 19, 12, 0, 0, TimeSpan.Zero);

    private readonly RangeApiInMemory _service = new();
    private readonly ConfigurationInMemory _configuration = new();
    private readonly ScreeningLogInMemory _log = new();
    private readonly EventsInMemory _events = new();

    private OfflineCorpus _offline = new();
    private WordList _words = new(DictionaryWords.Default);
    private DateTimeOffset _now = Noon;

    /// <summary>
    /// INT-PWD-001 AC1: the request carries the first five characters of the hash and
    /// nothing else, so the service is asked about a range and never about a password.
    /// </summary>
    [Fact]
    public async Task INT_PWD_001_AC1_TheRequestCarriesThePrefixOnlyAsync()
    {
        _service.Holds(Prefix(Password), Suffix(Password) + ":12");

        Assert.False(await AcceptedAsync());

        Uri asked = Assert.Single(_service.Asked);

        Assert.Equal(Prefix(Password), asked.Segments[^1]);
        Assert.EndsWith("/range/" + Prefix(Password), asked.AbsolutePath, StringComparison.Ordinal);
    }

    /// <summary>
    /// INT-PWD-001 AC2: no full hash appears anywhere in what goes out, which is what
    /// keeps the password unguessable to the service.
    /// </summary>
    [Fact]
    public async Task INT_PWD_001_AC2_NoFullHashAppearsInTheRequestAsync()
    {
        _service.Holds(Prefix(Password), Suffix(Password));

        Assert.False(await AcceptedAsync());

        Uri asked = Assert.Single(_service.Asked);

        Assert.DoesNotContain(Hash(Password), asked.AbsoluteUri, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(Suffix(Password), asked.AbsoluteUri, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(Password, asked.AbsoluteUri, StringComparison.Ordinal);
    }

    /// <summary>
    /// INT-PWD-001 AC3: every range request the client the library registers makes names
    /// the library's package and its version, the version being the constant the build
    /// writes and carrying no build metadata.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task INT_PWD_001_AC3_EveryRangeRequestNamesTheLibraryAndItsVersionAsync()
    {
        await using ServiceProvider deployed = new ServiceCollection()
            .AddSingleton<IEvents>(_events)
            .AddSingleton<IMailTransport>(new MailTransportInMemory())
            .AddSingleton<ISmsTransport>(new SmsTransportInMemory())
            .AddSingleton(new AuthenticationAddresses(
                "https://accounts.example.test/signin",
                "https://accounts.example.test"))
            .AddSingleton(new SignOnClient("this-application"))
            .AddJanus(
                Connection,
                new KeyEncryptionKeys(1, new Dictionary<int, ReadOnlyMemory<byte>> { [1] = new byte[32] }),
                new FingerprintKeys(1, new Dictionary<int, ReadOnlyMemory<byte>> { [1] = new byte[32] }),
                Encoding.UTF8.GetBytes("the secret this application presents"),
                Encoding.UTF8.GetBytes(Connection),
                HostFixture.Declaration(),
                ApplicationKind.Public)
            .Configure<HttpClientFactoryOptions>(
                nameof(ILeakedPasswordCorpus),
                options => options.HttpMessageHandlerBuilderActions.Add(
                    builder => builder.PrimaryHandler = _service))
            .BuildServiceProvider();

        ILeakedPasswordCorpus corpus = deployed.GetRequiredService<ILeakedPasswordCorpus>();

        foreach (string password in new[] { Password, Listed })
        {
            _service.Holds(Prefix(password), Suffix(password) + ":1");

            _ = await corpus.RangeAsync(
                BlocklistSource.RangeApi,
                Prefix(password),
                TestContext.Current.CancellationToken);
        }

        Assert.Equal(2, _service.Agents.Count);
        Assert.All(
            _service.Agents,
            agent => Assert.Equal(LibraryPackage.Identifier + "/" + LibraryPackage.Version, agent));
        Assert.DoesNotContain('+', LibraryPackage.Version);
    }

    /// <summary>
    /// INT-PWD-002 AC1 and AUTH-PASS-004 AC2: with the service unreachable the list
    /// the package carries answers, on a deployment that holds no file of its own, and
    /// the fall back is recorded so the degradation is visible rather than silent.
    /// </summary>
    [Fact]
    public async Task INT_PWD_002_AC1_WithTheServiceUnreachableTheOfflineListAnswersAsync()
    {
        _service.Reachable = false;

        Assert.False(await AcceptedAsync(Listed));
        Assert.Equal(
            [(BlocklistSource.RangeApi, BlocklistSource.Offline)],
            [.. _log.Entries]);
        Assert.Equal(
            [Alerts.Key(AlertCondition.Degradation, "password.blocklist.fallback")],
            _events.Of<AlertRaised>().Select(raised => Alerts.Deduplication(raised.IdempotencyKey)));
    }

    /// <summary>
    /// AUTH-PASS-004 AC2: the fall back screens rather than waves through, so a
    /// password the offline list does not hold is accepted on that list's word.
    /// </summary>
    [Fact]
    public async Task AUTH_PASS_004_AC2_TheOfflineListAcceptsWhatItDoesNotHoldAsync()
    {
        _service.Reachable = false;

        Assert.True(await AcceptedAsync());
        Assert.Single(_log.Entries);
    }

    /// <summary>
    /// INT-PWD-002 AC2: with the service unreachable and the list the package carries
    /// unreadable, the password is refused rather than accepted unscreened.
    /// </summary>
    [Fact]
    public async Task INT_PWD_002_AC2_WithNeitherAvailableTheOperationFailsAsync()
    {
        _service.Reachable = false;
        _offline = new OfflineCorpus(() => null);

        Assert.Equal(ErrorCodes.ScreeningUnavailable, await RefusalAsync());
    }

    /// <summary>
    /// INT-PWD-003 AC1: naming the self-hosted corpus and where it answers is the
    /// whole switch, and the deployment's own corpus is what is asked afterwards.
    /// </summary>
    [Fact]
    public async Task INT_PWD_003_AC1_SwitchingToTheSelfHostedCorpusIsConfigurationOnlyAsync()
    {
        _service.Holds(Prefix(Password), Suffix(Password) + ":3");
        _configuration.Set(Settings.PasswordBlocklistSource, BlocklistSource.SelfHosted);
        _configuration.Set(Settings.PasswordBlocklistSelfHostedAddress, SelfHosted);

        Assert.False(await AcceptedAsync());

        Uri asked = Assert.Single(_service.Asked);

        Assert.Equal(SelfHosted + "/" + Prefix(Password), asked.AbsoluteUri);
        Assert.Empty(_log.Entries);
    }

    /// <summary>
    /// INT-PWD-002 and INT-PWD-003: a deployment that names the self-hosted corpus and
    /// no address for it has nowhere to ask, so the list the package carries answers
    /// and the fall back is recorded rather than the password waved through.
    /// </summary>
    [Fact]
    public async Task ScreenAsync_TheSelfHostedCorpusWithNoAddress_FallsBackAsync()
    {
        _configuration.Set(Settings.PasswordBlocklistSource, BlocklistSource.SelfHosted);

        Assert.False(await AcceptedAsync(Listed));
        Assert.Empty(_service.Asked);
        Assert.Equal(
            [(BlocklistSource.SelfHosted, BlocklistSource.Offline)],
            [.. _log.Entries]);
    }

    /// <summary>
    /// INF-TLS-004 AC1: a self-hosted corpus address written over plain HTTP after
    /// startup checked it is never asked; the list the package carries answers and the
    /// fall back is recorded.
    /// </summary>
    [Fact]
    public async Task INF_TLS_004_AC1_APlaintextCorpusAddressIsNeverAskedAsync()
    {
        _service.Holds(Prefix(Password), Suffix(Password) + ":3");
        _configuration.Set(Settings.PasswordBlocklistSource, BlocklistSource.SelfHosted);
        _configuration.Set(Settings.PasswordBlocklistSelfHostedAddress, "http://corpus.example/range");

        Assert.False(await AcceptedAsync(Listed));
        Assert.Empty(_service.Asked);
        Assert.Equal(
            [(BlocklistSource.SelfHosted, BlocklistSource.Offline)],
            [.. _log.Entries]);
    }

    /// <summary>
    /// A corpus older than the deployment admits cannot be screened on, so it answers
    /// nothing and the password is refused rather than judged against stale data.
    /// </summary>
    [Fact]
    public async Task ScreenAsync_ACorpusOlderThanTheMaximumAge_RefusesAsync()
    {
        _service.Reachable = false;
        _now = Noon.AddYears(10);

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
        _offline = new OfflineCorpus(() => Written(Hash(Password) + Line));

        Assert.Equal(ErrorCodes.ScreeningUnavailable, await RefusalAsync());
    }

    /// <summary>
    /// AUTH-PASS-004: a deployment that rejects on the word lists and cannot open one
    /// refuses the password, because a source that cannot answer never answers yes.
    /// </summary>
    [Fact]
    public async Task ScreenAsync_TheDictionarySourceWithAListItCannotOpen_RefusesAsync()
    {
        _service.Holds(Prefix(Password), "0000000000000000000000000000000000000");
        _words = new WordList(DictionaryWords.Default, _ => null);
        RejectingOnTheDictionary();

        Assert.Equal(ErrorCodes.ScreeningUnavailable, await RefusalAsync());
    }

    /// <summary>
    /// AUTH-PASS-004: a word the English list holds is a word the password is refused
    /// for, where the deployment rejects on the lists.
    /// </summary>
    [Fact]
    public async Task ScreenAsync_TheDictionarySourceAndAListedWord_RefusesAsync()
    {
        _service.Holds(Prefix(Password), "0000000000000000000000000000000000000");
        RejectingOnTheDictionary();

        Assert.Equal(ErrorCodes.PasswordBlocklisted, await RefusalAsync());
    }

    /// <summary>
    /// AUTH-PASS-004 AC8: a password built on an Arabizi form is refused, and nothing the
    /// refusal, the events or the screening log carry names the word it matched.
    /// </summary>
    [Fact]
    public async Task AUTH_PASS_004_AC8_AnArabiziFormIsRefusedAndNothingNamesTheWordAsync()
    {
        const string arabizi = "zq 7abibi zq";
        const string matched = "7abibi";

        _service.Holds(Prefix(arabizi), "0000000000000000000000000000000000000");
        RejectingOnTheDictionary();

        Error refusal = (await ScreenedAsync(arabizi)).Match(
            () => throw new Xunit.Sdk.XunitException("The password was accepted."),
            error => error);

        Assert.Equal(ErrorCodes.PasswordBlocklisted, refusal.Code);
        Assert.Empty(refusal.Details);
        Assert.All(_events.Published, raised => Assert.DoesNotContain(
            matched,
            raised.ToString(),
            StringComparison.OrdinalIgnoreCase));
        Assert.Empty(_log.Entries);
    }

    /// <inheritdoc/>
    public void Dispose() => _service.Dispose();

    // The corpus is published as ranges of this hash (INT-PWD-001), so the test
    // computes the same lookup key the screening does; it is no security claim.
#pragma warning disable CA5350 // The corpus is published as SHA-1 ranges (INT-PWD-001); the value is a lookup key.
    private static string Hash(string password) =>
        Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes(password)));
#pragma warning restore CA5350

    private static string Prefix(string password) => Hash(password)[..5];

    private static string Suffix(string password) => Hash(password)[5..];

    private static MemoryStream Written(string contents) =>
        new MemoryStream(Encoding.UTF8.GetBytes(contents));

    private async Task<bool> AcceptedAsync(string password = Password) =>
        (await ScreenedAsync(password)).Match(() => true, _ => false);

    private async Task<ErrorCode> RefusalAsync(string password = Password) =>
        (await ScreenedAsync(password)).Match(
            () => throw new Xunit.Sdk.XunitException("The password was accepted."),
            error => error.Code);

    private async Task<Result> ScreenedAsync(string password)
    {
        using var client = new HttpClient(_service, disposeHandler: false)
        {
            BaseAddress = LeakedPasswordCorpus.Provider,
        };

        var screening = new PasswordScreening(
            new LeakedPasswordCorpus(client, _configuration, new FixedTime(_now), _offline),
            _words,
            _configuration,
            _log,
            _events,
            new FixedTime(_now));

        return await screening.ScreenAsync(
            Encoding.UTF8.GetBytes(password),
            [],
            TestContext.Current.CancellationToken);
    }

    private void RejectingOnTheDictionary() =>
        _configuration.Set<IReadOnlySet<BlocklistRejectionSource>>(
            Settings.PasswordBlocklistSources,
            new HashSet<BlocklistRejectionSource>
            {
                BlocklistRejectionSource.Leaked,
                BlocklistRejectionSource.Dictionary,
            });
}
