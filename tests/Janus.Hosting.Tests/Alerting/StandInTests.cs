using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Janus.Authentication.Alerting;
using Janus.Authentication.Sessions;
using Janus.Authentication.Tests;
using Janus.Core;
using Janus.Core.Configuration;
using Janus.Hosting.Sending;
using Janus.Hosting.Sessions;
using Janus.Testing;
using Xunit;

namespace Janus.Hosting.Tests.Alerting;

/// <summary>
/// The testing package's stand-ins for the optional environment seams, read by what the
/// library runs over each seam (LIB-TEST-003, INT-GEN-006, AUTH-ABUSE-008).
/// </summary>
[Trait("kind", "unit")]
public sealed class StandInTests
{
    private static readonly AccessContext Watcher = AccessContext.Of(
        SystemPrincipal.ForDeployment("stand-ins", "LIB-TEST-003", SystemOperation.Monitoring));

    private static readonly DateTimeOffset Noon = new(2026, 10, 20, 12, 0, 0, TimeSpan.Zero);

    private readonly ConfigurationInMemory _configuration = new();
    private readonly EventsInMemory _events = new();
    private readonly FixedClock _clock = new(Noon);

    /// <summary>
    /// LIB-TEST-003 AC4: a deployment composed with the two stand-ins, the datacenter
    /// range signal counted, raises no degradation and no other alert from the location
    /// file or the range file, on a refresh or on a read.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task LIB_TEST_003_AC4_TheStandInsRaiseNoDegradationAsync()
    {
        _configuration.Set(
            Settings.AbuseBotDefenceSignals,
            (IReadOnlySet<BotDefenceSignal>)new HashSet<BotDefenceSignal> { BotDefenceSignal.DatacenterRange });

        var location = new LocationDatabase(new LocationCopy(), new EmptyLocationSource(_clock), _configuration, _events, _clock);
        var ranges = new DatacenterRanges(new DatacenterRangeCopy(), new EmptyDatacenterRangeSource(_clock), _configuration, _events, _clock);

        Succeeded(await location.RefreshAsync(Watcher, TestContext.Current.CancellationToken));
        Succeeded(await ranges.RefreshAsync(Watcher, TestContext.Current.CancellationToken));

        Result<ResolvedLocation?> resolved = await location.ResolveAsync("198.51.100.7", TestContext.Current.CancellationToken);
        Result<bool> contained = await ranges.ContainsAsync("198.51.100.7", TestContext.Current.CancellationToken);

        Assert.Null(resolved.Match(found => found, error => throw new Xunit.Sdk.XunitException($"The read was refused: {error.Code}.")));
        Assert.False(contained.Match(inside => inside, error => throw new Xunit.Sdk.XunitException($"The ask was refused: {error.Code}.")));
        Assert.Empty(_events.Of<AlertRaised>());
    }

    private static void Succeeded(Result run) =>
        run.Switch(() => { }, error => throw new Xunit.Sdk.XunitException($"The run failed: {error.Code}."));
}
