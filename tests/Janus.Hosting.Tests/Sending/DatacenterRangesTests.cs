using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Janus.Authentication.Alerting;
using Janus.Authentication.Tests;
using Janus.Core;
using Janus.Core.Configuration;
using Janus.Hosting.Background;
using Janus.Hosting.Sending;
using Xunit;

namespace Janus.Hosting.Tests.Sending;

/// <summary>
/// What the datacenter range signal is matched against: the file the deployment
/// supplies, read in process and held, refreshed by a job, refused whole where it
/// cannot be read, and raised as a degradation while none is held or the one held is
/// stale (AUTH-ABUSE-008).
/// </summary>
[Trait("kind", "unit")]
public sealed class DatacenterRangesTests
{
    private const string Job = "datacenter-ranges";
    private const string Absent = "botdefence.ranges.absent";
    private const string Stale = "botdefence.ranges.stale";

    private static readonly AccessContext Watcher = AccessContext.Of(
        SystemPrincipal.ForDeployment(Job, "AUTH-ABUSE-008", SystemOperation.Monitoring));

    private static readonly DateTimeOffset Noon = new(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);

    private static readonly string Listed = string.Join(
        '\n',
        "# 2026-03-01",
        "198.51.100.0\t198.51.100.255",
        "203.0.113.0\t203.0.113.255",
        "2001:db8:5::\t2001:db8:5:ffff:ffff:ffff:ffff:ffff");

    private readonly DatacenterRangeCopy _copy = new();
    private readonly DatacenterRangeSourceInMemory _source = new() { Text = Listed };
    private readonly ConfigurationInMemory _configuration = new();
    private readonly EventsInMemory _events = new();
    private readonly FixedClock _clock = new(Noon);

    private DatacenterRanges Ranges => new(_copy, _source, _configuration, _events, _clock);

    private DatacenterRanges Unsupplied => new(_copy, source: null, _configuration, _events, _clock);

    /// <summary>
    /// AUTH-ABUSE-008: the ranges hold nothing they could reach a third party with: the
    /// copy in memory, the file the deployment holds, the settings, the alerts and the
    /// clock. The library ships no ranges and fetches none.
    /// </summary>
    [Fact]
    public void AUTH_ABUSE_008_TheRangesHoldNothingTheyCouldCallOutWith()
    {
        ParameterInfo[] held = typeof(DatacenterRanges)
            .GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .Single()
            .GetParameters();

        Assert.Equal(
            [typeof(DatacenterRangeCopy), typeof(IDatacenterRangeSource), typeof(IConfigurationStore), typeof(IAlertChannels), typeof(TimeProvider)],
            held.Select(parameter => parameter.ParameterType));
    }

    /// <summary>
    /// AUTH-ABUSE-008 AC3: an address is matched against the ranges of the host's file,
    /// in either family and at both ends of a range, an IPv4-mapped address against its
    /// IPv4 range, and an address in no range or that does not read as one is matched
    /// by none; with a fresh file nothing is raised.
    /// </summary>
    /// <param name="address">The address the request arrived on.</param>
    /// <param name="inside">Whether the file holds it.</param>
    /// <returns>The work of the test.</returns>
    [Theory]
    [InlineData("198.51.100.0", true)]
    [InlineData("198.51.100.255", true)]
    [InlineData("203.0.113.9", true)]
    [InlineData("::ffff:203.0.113.9", true)]
    [InlineData("2001:db8:5:2::9", true)]
    [InlineData("198.51.101.0", false)]
    [InlineData("2001:db8:6::1", false)]
    [InlineData("unknown", false)]
    public async Task AUTH_ABUSE_008_AC3_AnAddressIsMatchedAgainstTheRangesOfTheHostsFileAsync(
        string address,
        bool inside)
    {
        Assert.Equal(inside, await ContainedAsync(Ranges, address));
        Assert.Empty(_events.Of<AlertRaised>());
    }

    /// <summary>
    /// AUTH-ABUSE-008: every address is matched against the copy the process holds, so
    /// the file is opened once however many addresses are judged.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_ABUSE_008_EveryAddressIsMatchedAgainstTheCopyHeldAsync()
    {
        foreach (string address in new[] { "198.51.100.7", "203.0.113.9", "2001:db8:5::1", "192.0.2.4" })
        {
            _ = await ContainedAsync(Ranges, address);
        }

        Assert.Equal(1, _source.Opened);
    }

    /// <summary>
    /// AUTH-ABUSE-008 AC3, LIB-HOST-001 AC7: with no range source declared the signal
    /// does not fire and <c>degradation</c> is raised under
    /// <c>botdefence.ranges.absent</c> (chapter 10 section 5.23), each time it is asked,
    /// so the router carries it once a window.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_ABUSE_008_AC3_WithNoRangeSourceTheSignalDoesNotFireAndDegradationIsRaisedAsync()
    {
        for (int registration = 0; registration < 3; registration++)
        {
            Assert.False(await ContainedAsync(Unsupplied, "198.51.100.7"));
        }

        Assert.Equal(3, _events.Of<AlertRaised>().Count);
        Assert.All(_events.Of<AlertRaised>(), raised => Degraded(Absent, raised));
    }

    /// <summary>
    /// AUTH-ABUSE-008 AC3: a source that cannot open its file, with no copy read before,
    /// leaves no range file read: the signal does not fire and the absence is raised.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_ABUSE_008_AC3_WithNoFileReadTheSignalDoesNotFireAndDegradationIsRaisedAsync()
    {
        _source.Refusal = Error.From(ErrorCodes.RequestMalformed);

        Assert.False(await ContainedAsync(Ranges, "198.51.100.7"));
        Degraded(Absent, Assert.Single(_events.Of<AlertRaised>()));
    }

    /// <summary>
    /// AUTH-ABUSE-008 AC3: a file older than <c>abuse.botdefence.ranges.maxage</c>,
    /// judged from its own date, is stale: the signal does not fire and
    /// <c>degradation</c> is raised under <c>botdefence.ranges.stale</c>; a file exactly
    /// that old is fresh, and a deployment that admits an older file is answered from
    /// it.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_ABUSE_008_AC3_AFileOlderThanItsMaximumAgeDoesNotFireAndIsRaisedStaleAsync()
    {
        _source.Text = Listed.Replace("# 2026-03-01", "# 2026-01-30", StringComparison.Ordinal);

        Assert.True(await ContainedAsync(Ranges, "198.51.100.7"));
        Assert.Empty(_events.Of<AlertRaised>());

        _clock.Advance(TimeSpan.FromDays(1));

        Assert.False(await ContainedAsync(Ranges, "198.51.100.7"));
        Degraded(Stale, Assert.Single(_events.Of<AlertRaised>()));

        _configuration.Set(Settings.AbuseBotDefenceRangesMaxAge, TimeSpan.FromDays(90));

        Assert.True(await ContainedAsync(Ranges, "198.51.100.7"));
        _ = Assert.Single(_events.Of<AlertRaised>());
    }

    /// <summary>
    /// AUTH-ABUSE-008 AC3: the refresh runs from the job with nobody asking, and what it
    /// reads replaces the copy held.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_ABUSE_008_AC3_ARefreshReplacesTheCopyHeldAsync()
    {
        Assert.False(await ContainedAsync(Ranges, "192.0.2.4"));

        _source.Text = Listed + "\n192.0.2.0\t192.0.2.255";

        await Ranges.RefreshAsync(Watcher, TestContext.Current.CancellationToken);

        Assert.True(await ContainedAsync(Ranges, "192.0.2.4"));
        Assert.Equal(2, _source.Opened);
        Assert.Empty(_events.Of<AlertRaised>());
    }

    /// <summary>
    /// AUTH-ABUSE-008 AC3: the range file is refreshed by the job
    /// <c>datacenter-ranges</c>, a principal that may monitor (chapter 10 section 5.29),
    /// every <c>abuse.botdefence.ranges.refresh</c>, a day unless the deployment says
    /// otherwise.
    /// </summary>
    [Fact]
    public void AUTH_ABUSE_008_AC3_TheRangeFileIsRefreshedByItsJobEveryRefreshInterval()
    {
        BackgroundJob job = BackgroundJobs.All.Single(candidate => candidate.Name == Job);

        Assert.Equal("AUTH-ABUSE-008", job.Principal.Reason);
        Assert.True(job.Principal.MayRun(SystemOperation.Monitoring));
        Assert.Equal(Settings.AbuseBotDefenceRangesRefresh.Default, job.Fallback);
        Assert.Equal(TimeSpan.FromDays(1), Settings.AbuseBotDefenceRangesRefresh.Default);
        Assert.Equal(TimeSpan.FromDays(30), Settings.AbuseBotDefenceRangesMaxAge.Default);
    }

    /// <summary>
    /// IDN-PRIN-001 AC3: the refresh refuses a principal that may not monitor before it
    /// opens the file.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_PRIN_001_AC3_TheRefreshRefusesAPrincipalThatMayNotMonitorAsync()
    {
        var other = AccessContext.Of(
            SystemPrincipal.ForDeployment("bootstrap", "OPS-BOOT-001", SystemOperation.Bootstrap));

        ArgumentException refused = await Assert.ThrowsAsync<ArgumentException>(
            async () => await Ranges.RefreshAsync(other, TestContext.Current.CancellationToken));

        Assert.Equal("context", refused.ParamName);
        Assert.Equal(0, _source.Opened);
    }

    /// <summary>
    /// AUTH-ABUSE-008: a failed refresh keeps the copy held, which goes on answering
    /// until it is stale, and then the signal does not fire and the staleness is raised.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_ABUSE_008_AFailedRefreshKeepsTheCopyHeldUntilItIsStaleAsync()
    {
        Assert.True(await ContainedAsync(Ranges, "198.51.100.7"));

        _source.Refusal = Error.From(ErrorCodes.RequestMalformed);

        await Ranges.RefreshAsync(Watcher, TestContext.Current.CancellationToken);

        Assert.True(await ContainedAsync(Ranges, "198.51.100.7"));
        Assert.Empty(_events.Of<AlertRaised>());

        _clock.Advance(TimeSpan.FromDays(31));

        await Ranges.RefreshAsync(Watcher, TestContext.Current.CancellationToken);

        Assert.False(await ContainedAsync(Ranges, "198.51.100.7"));
        Degraded(Stale, Assert.Single(_events.Of<AlertRaised>()));
    }

    /// <summary>
    /// AUTH-ABUSE-008: a file read in part would match some addresses and silently not
    /// others, so a file with a line that cannot be read, or a range of mixed family,
    /// reversed or overlapping another, is refused whole and the copy held before it
    /// kept.
    /// </summary>
    /// <param name="refused">The line that makes the file unreadable.</param>
    /// <returns>The work of the test.</returns>
    [Theory]
    [InlineData("192.0.2.0")]
    [InlineData("192.0.2.0\t192.0.2.255\tEG")]
    [InlineData("192.0.2.0 192.0.2.255")]
    [InlineData("192.0.2.0\tthe last address")]
    [InlineData("192.0.2.0/24\t192.0.2.255")]
    [InlineData("192.0.2.0\t2001:db8:1::ffff")]
    [InlineData("192.0.2.255\t192.0.2.0")]
    [InlineData("198.51.100.128\t198.51.101.0")]
    [InlineData("::ffff:203.0.113.0\t::ffff:203.0.113.9")]
    public async Task AUTH_ABUSE_008_AFileThatCannotBeReadWholeIsRefusedAsync(string refused)
    {
        Assert.True(await ContainedAsync(Ranges, "198.51.100.7"));

        _source.Text = Listed.Replace("198.51.100.0\t198.51.100.255", "198.51.100.0\t198.51.100.127", StringComparison.Ordinal)
            + "\n" + refused;

        await Ranges.RefreshAsync(Watcher, TestContext.Current.CancellationToken);

        Assert.True(await ContainedAsync(Ranges, "198.51.100.200"));
        Assert.Equal(2, _source.Opened);
    }

    /// <summary>
    /// AUTH-ABUSE-008: a file that does not say when it was produced cannot be judged
    /// against <c>abuse.botdefence.ranges.maxage</c>, and is refused whole: none is
    /// held, the signal does not fire and the absence is raised.
    /// </summary>
    /// <param name="first">The first line of the file.</param>
    /// <returns>The work of the test.</returns>
    [Theory]
    [InlineData("# produced this spring")]
    [InlineData("# 2026-3-1")]
    [InlineData("192.0.2.0\t192.0.2.255")]
    public async Task AUTH_ABUSE_008_AFileWithNoDateIsRefusedAsync(string first)
    {
        _source.Text = Listed.Replace("# 2026-03-01", first, StringComparison.Ordinal);

        Assert.False(await ContainedAsync(Ranges, "198.51.100.7"));
        Degraded(Absent, Assert.Single(_events.Of<AlertRaised>()));
    }

    private static void Degraded(string scope, AlertRaised raised)
    {
        Assert.Equal(AlertCondition.Degradation, raised.Condition);
        Assert.Equal(scope, raised.Scope);
        Assert.Equal(Alerts.Key(AlertCondition.Degradation, scope, named: null), Alerts.Deduplication(raised.IdempotencyKey));
    }

    private static async Task<bool> ContainedAsync(DatacenterRanges ranges, string address) =>
        (await ranges.ContainsAsync(address, TestContext.Current.CancellationToken)).Match(
            inside => inside,
            error => throw new Xunit.Sdk.XunitException($"The ask was refused: {error.Code}."));
}
