using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Janus.Authentication.Alerting;
using Janus.Authentication.Sessions;
using Janus.Authentication.Tests;
using Janus.Core;
using Janus.Core.Configuration;
using Janus.Hosting.Sessions;
using Xunit;

namespace Janus.Hosting.Tests.Sessions;

/// <summary>
/// What a session's city is resolved from: the file the deployment supplies, read in
/// process and held, refreshed by a job and refused whole where it cannot be read
/// (INT-GEN-006).
/// </summary>
[Trait("kind", "unit")]
public sealed class LocationDatabaseTests
{
    private static readonly DateTimeOffset Noon = new(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);

    private static readonly string Listed = string.Join(
        '\n',
        "# 2026-03-01",
        "198.51.100.0\t198.51.100.255\tEG\tCairo\t30.0444\t31.2357",
        "203.0.113.0\t203.0.113.255\tGB\tLondon\t51.5072\t-0.1276",
        "2001:db8::\t2001:db8::ffff\tEG\tAlexandria\t31.2001\t29.9187",
        "192.0.2.0\t192.0.2.255\tEG\t\t\t");

    private readonly LocationCopy _copy = new();
    private readonly LocationSourceInMemory _source = new() { Text = Listed };
    private readonly ConfigurationInMemory _configuration = new();
    private readonly EventsInMemory _events = new();
    private readonly FixedClock _clock = new(Noon);

    private LocationDatabase Database => new(_copy, _source, _configuration, _events, _clock);

    private LocationDatabase Unsupplied => new(_copy, source: null, _configuration, _events, _clock);

    /// <summary>
    /// INT-GEN-006 AC1: the resolver holds nothing it could reach a third party with:
    /// the copy in memory, the file the deployment holds, the settings, the alerts and
    /// the clock.
    /// </summary>
    [Fact]
    public void INT_GEN_006_AC1_TheResolverHoldsNothingItCouldCallOutWith()
    {
        ParameterInfo[] held = typeof(LocationDatabase)
            .GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .Single()
            .GetParameters();

        Assert.Equal(
            [typeof(LocationCopy), typeof(ILocationSource), typeof(IConfigurationStore), typeof(IAlertChannels), typeof(TimeProvider)],
            held.Select(parameter => parameter.ParameterType));
    }

    /// <summary>
    /// INT-GEN-006 AC1: every address is resolved against the copy the process holds,
    /// so the file is opened once however many addresses are resolved.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task INT_GEN_006_AC1_EveryAddressIsResolvedAgainstTheCopyHeldAsync()
    {
        foreach (string address in new[] { "198.51.100.7", "203.0.113.9", "2001:db8::1", "192.0.2.4" })
        {
            _ = await ResolvedAsync(Database, address);
        }

        Assert.Equal(1, _source.Opened);
    }

    /// <summary>
    /// INT-GEN-006 AC3, AUTH-SESS-013: an address is resolved to the city and country
    /// of the range that holds it, in either family, and to nothing finer.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task INT_GEN_006_AC3_TheCityIsWhatTheFileSaysOfTheAddressAsync()
    {
        Assert.Equal(
            new ResolvedLocation(new SessionLocation("Cairo", "EG"), new Coordinates(30.0444, 31.2357)),
            await ResolvedAsync(Database, "198.51.100.7"));
        Assert.Equal(
            new ResolvedLocation(new SessionLocation("Alexandria", "EG"), new Coordinates(31.2001, 29.9187)),
            await ResolvedAsync(Database, "2001:db8::1"));
        Assert.Equal(
            new SessionLocation("London", "GB"),
            (await ResolvedAsync(Database, "::ffff:203.0.113.9"))?.Location);
        Assert.Equal(
            new ResolvedLocation(new SessionLocation(City: null, "EG"), Coordinates: null),
            await ResolvedAsync(Database, "192.0.2.4"));
        Assert.Null(await ResolvedAsync(Database, "100.64.0.1"));
        Assert.Empty(_events.Of<AlertRaised>());
    }

    /// <summary>
    /// INT-GEN-006 AC3: with no file available the resolver answers no location.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task INT_GEN_006_AC3_WithNoFileAvailableNoLocationIsAnsweredAsync() =>
        Assert.Null(await ResolvedAsync(Unsupplied, "198.51.100.7"));

    /// <summary>
    /// INT-GEN-006 AC2: the missing file surfaces as the degradation condition, under
    /// a scope of its own so that the router carries it once a window and not once a
    /// sign-in, and not under another degradation's key.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task INT_GEN_006_AC2_TheMissingFileSurfacesAsADegradationAsync()
    {
        for (int session = 0; session < 3; session++)
        {
            _ = await ResolvedAsync(Unsupplied, "198.51.100.7");
        }

        Assert.Equal(3, _events.Of<AlertRaised>().Count);
        Assert.All(_events.Of<AlertRaised>(), raised => Degraded("location.database.absent", raised));
    }

    /// <summary>
    /// INT-GEN-006 AC2: a refresh that could not open the file surfaces as a
    /// degradation, and the copy held before it goes on answering.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task INT_GEN_006_AC2_AFailedRefreshSurfacesAsADegradationAsync()
    {
        _ = await ResolvedAsync(Database, "198.51.100.7");

        _source.Refusal = Error.From(ErrorCodes.RequestMalformed);

        await RefreshedAsync();

        Degraded("location.database.refresh", Assert.Single(_events.Of<AlertRaised>()));
        Assert.Equal(new SessionLocation("Cairo", "EG"), (await ResolvedAsync(Database, "198.51.100.7"))?.Location);
    }

    /// <summary>
    /// INT-GEN-006 AC2: the refresh runs from the job with nobody asking, and what it
    /// reads replaces the copy held.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task INT_GEN_006_AC2_ARefreshReplacesTheCopyHeldAsync()
    {
        _ = await ResolvedAsync(Database, "198.51.100.7");

        _source.Text = Listed.Replace("Cairo\t30.0444\t31.2357", "Giza\t30.0131\t31.2089", StringComparison.Ordinal);

        await RefreshedAsync();

        Assert.Equal(new SessionLocation("Giza", "EG"), (await ResolvedAsync(Database, "198.51.100.7"))?.Location);
        Assert.Equal(2, _source.Opened);
    }

    /// <summary>
    /// INT-GEN-006: a file read in part would resolve some addresses and silently not
    /// others, so a file with no date, a line that cannot be read or two ranges that
    /// overlap is refused whole, raised, and the copy held before it kept.
    /// </summary>
    /// <param name="refused">The line that makes the file unreadable.</param>
    /// <returns>The work of the test.</returns>
    [Theory]
    [InlineData("198.51.100.0\t198.51.100.255\tEG\tCairo")]
    [InlineData("100.64.0.0\t100.64.0.255\tEG\tCairo\t\t")]
    [InlineData("100.64.0.0\t100.64.0.255\tEG\tCairo\t91\t31.2357")]
    [InlineData("100.64.0.0\t100.64.0.255\tEGY\tCairo\t30.0444\t31.2357")]
    [InlineData("100.64.0.0\t2001:db8:1::ffff\tEG\tCairo\t30.0444\t31.2357")]
    [InlineData("100.64.0.255\t100.64.0.0\tEG\tCairo\t30.0444\t31.2357")]
    [InlineData("198.51.100.128\t198.51.101.0\tEG\tGiza\t30.0131\t31.2089")]
    public async Task INT_GEN_006_AFileThatCannotBeReadWholeIsRefusedAsync(string refused)
    {
        _ = await ResolvedAsync(Database, "198.51.100.7");

        _source.Text = Listed + "\n" + refused;

        await RefreshedAsync();

        Degraded("location.database.refresh", Assert.Single(_events.Of<AlertRaised>()));
        Assert.Equal(new SessionLocation("Cairo", "EG"), (await ResolvedAsync(Database, "198.51.100.7"))?.Location);
    }

    /// <summary>
    /// INT-GEN-006: a file that does not say when it was produced cannot be judged
    /// against <c>location.database.maxage</c>, and is refused.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task INT_GEN_006_AFileWithNoDateIsRefusedAsync()
    {
        _source.Text = Listed.Replace("# 2026-03-01", "# produced this spring", StringComparison.Ordinal);

        Assert.Null(await ResolvedAsync(Database, "198.51.100.7"));
        Assert.Equal(
            [Alerts.Key(AlertCondition.Degradation, "location.database.refresh"), Alerts.Key(AlertCondition.Degradation, "location.database.absent")],
            _events.Of<AlertRaised>().Select(raised => Alerts.Deduplication(raised.IdempotencyKey)));
    }

    /// <summary>
    /// INT-GEN-006: a file older than <c>location.database.maxage</c> is stale, no
    /// location is shown and the degradation is raised; a deployment that admits an
    /// older file is answered from it.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task INT_GEN_006_AStaleFileAnswersNoLocationAndIsRaisedAsync()
    {
        _source.Text = Listed.Replace("# 2026-03-01", "# 2026-01-01", StringComparison.Ordinal);

        Assert.Null(await ResolvedAsync(Database, "198.51.100.7"));
        Degraded("location.database.stale", Assert.Single(_events.Of<AlertRaised>()));

        _configuration.Set(Settings.LocationDatabaseMaxAge, TimeSpan.FromDays(90));

        Assert.Equal(new SessionLocation("Cairo", "EG"), (await ResolvedAsync(Database, "198.51.100.7"))?.Location);
    }

    private static void Degraded(string scope, AlertRaised raised)
    {
        Assert.Equal(AlertCondition.Degradation, raised.Condition);
        Assert.Equal(Alerts.Key(AlertCondition.Degradation, scope), Alerts.Deduplication(raised.IdempotencyKey));
    }

    private static async Task<ResolvedLocation?> ResolvedAsync(LocationDatabase database, string address) =>
        (await database.ResolveAsync(address, TestContext.Current.CancellationToken)).Match(
            place => place,
            error => throw new Xunit.Sdk.XunitException($"The resolve was refused: {error.Code}."));

    private async Task RefreshedAsync() =>
        (await Database.RefreshAsync(TestContext.Current.CancellationToken)).Switch(
            () => { },
            error => throw new Xunit.Sdk.XunitException($"The refresh was refused: {error.Code}."));
}
