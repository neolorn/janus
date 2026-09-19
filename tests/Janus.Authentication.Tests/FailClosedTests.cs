using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Janus.Authentication.Passwords;
using Janus.Authentication.Tests.Passwords;
using Janus.Core;
using Janus.Core.Configuration;
using Xunit;

namespace Janus.Authentication.Tests;

/// <summary>
/// That authentication denies on anything it cannot decide (AUTH-PRIN-001).
/// </summary>
[Trait("kind", "contract")]
public sealed class FailClosedTests
{
    private readonly LeakedPasswordCorpusInMemory _corpus = new();
    private readonly WordListInMemory _words = new();
    private readonly ConfigurationInMemory _configuration = new();
    private readonly ScreeningLogInMemory _log = new();

    /// <summary>
    /// AUTH-PRIN-001 AC1: a cache outage never permits, and never denies either,
    /// because nothing the library reads to decide a request comes from one. The
    /// durable store is the only source, so losing the cache costs latency and
    /// nothing else (INF-CACHE-001).
    /// </summary>
    [Fact]
    public void AUTH_PRIN_001_AC1_NothingIsDecidedFromACache() =>
        Assert.Empty(Files(line =>
            line.Contains("IDistributedCache", StringComparison.Ordinal)
            || line.Contains("IMemoryCache", StringComparison.Ordinal)
            || line.Contains("MemoryCache(", StringComparison.Ordinal)));

    /// <summary>
    /// AUTH-PRIN-001 AC2: a blocklist that cannot answer refuses the operation rather
    /// than letting it through unscreened.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task AUTH_PRIN_001_AC2_ABlocklistOutageDoesNotSkipScreeningAsync()
    {
        _corpus.Unreachable.Add(BlocklistSource.RangeApi);
        _corpus.Unreachable.Add(BlocklistSource.Offline);

        Result screened = await new PasswordScreening(_corpus, _words, _configuration, _log)
            .ScreenAsync(
                Encoding.UTF8.GetBytes("orangemarmaladeandtoast"),
                [],
                TestContext.Current.CancellationToken);

        Assert.Equal(
            ErrorCodes.ScreeningUnavailable,
            screened.Match<ErrorCode?>(() => null, error => error.Code));
    }

    /// <summary>
    /// AUTH-PRIN-001 AC3: no code path returns an allow on an exception. An exception
    /// is caught only where an outage has to become a refusal (INT-PWD-002 AC1), and
    /// every such block answers with a failure or throws; JAN0006 forbids the rest.
    /// </summary>
    [Fact]
    public void AUTH_PRIN_001_AC3_NoPathReturnsAnAllowOnAnException() => Assert.Empty(Allowing());

    // The catch blocks that answer with anything but a refusal or an exception.
    private static string[] Allowing() =>
    [
        .. Catches().Where(caught =>
            caught.Contains("Result.Success", StringComparison.Ordinal)
            || !(caught.Contains("Result.Failure", StringComparison.Ordinal)
                || caught.Contains("throw", StringComparison.Ordinal))),
    ];

    // The shipped files in which a line says something, as the repository lays them
    // out.
    private static string[] Files(Func<string, bool> says) =>
    [
        .. Shipped()
            .Where(file => File.ReadLines(file).Any(says))
            .Select(file => Path.GetRelativePath(Path.Combine(Root(), "src"), file))
            .Order(StringComparer.Ordinal),
    ];

    // Every catch block the shipped files hold, as the text its braces enclose.
    private static IEnumerable<string> Catches()
    {
        foreach (string file in Shipped())
        {
            string text = File.ReadAllText(file);

            for (int at = text.IndexOf("catch", StringComparison.Ordinal); at >= 0;)
            {
                int opened = text.IndexOf('{', at);
                int closed = Closing(text, opened);

                yield return text[opened..closed];

                at = text.IndexOf("catch", closed, StringComparison.Ordinal);
            }
        }
    }

    // Where the brace that closes the one at this position is.
    private static int Closing(string text, int opened)
    {
        int depth = 0;

        for (int at = opened; at < text.Length; at++)
        {
            depth += text[at] switch
            {
                '{' => 1,
                '}' => -1,
                _ => 0,
            };

            if (depth is 0)
            {
                return at;
            }
        }

        throw new InvalidOperationException("The catch block is not closed.");
    }

    // The files the packages ship. The analyser project is not among them and reasons
    // about the constructs it forbids elsewhere (JAN0001, CONV-ERR-002).
    private static string[] Shipped() =>
    [
        .. Directory
            .GetFiles(Path.Combine(Root(), "src"), "*.cs", SearchOption.AllDirectories)
            .Where(file => !file.Contains(
                Path.Combine("src", "Janus.Analyzers"),
                StringComparison.Ordinal)),
    ];

    private static string Root()
    {
        var at = new DirectoryInfo(AppContext.BaseDirectory);

        while (at is not null && !Directory.Exists(Path.Combine(at.FullName, "src")))
        {
            at = at.Parent;
        }

        return at!.FullName;
    }
}
