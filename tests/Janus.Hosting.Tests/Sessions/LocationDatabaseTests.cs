using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Janus.Authentication.Alerting;
using Janus.Authentication.Tests;
using Janus.Core;
using Janus.Hosting.Sessions;
using Xunit;

namespace Janus.Hosting.Tests.Sessions;

/// <summary>
/// What a session's city is resolved from: a local database read in process, which no
/// deployment holds yet, so no location is shown and the degradation is raised
/// (INT-GEN-006).
/// </summary>
[Trait("kind", "unit")]
public sealed class LocationDatabaseTests
{
    private static readonly DateTimeOffset Noon = new(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly EventsInMemory _events = new();
    private readonly FixedClock _clock = new(Noon);

    private LocationDatabase Database => new(_events, _clock);

    /// <summary>
    /// INT-GEN-006 AC1: the resolver holds nothing it could reach a third party with,
    /// so no address a person signs in from can leave the deployment.
    /// </summary>
    [Fact]
    public void INT_GEN_006_AC1_TheResolverHoldsNothingItCouldCallOutWith()
    {
        ParameterInfo[] held = typeof(LocationDatabase)
            .GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .Single()
            .GetParameters();

        Assert.Equal(
            [typeof(IEvents), typeof(TimeProvider)],
            held.Select(parameter => parameter.ParameterType));
    }

    /// <summary>
    /// INT-GEN-006 AC3: with no file available the resolver answers no location.
    /// </summary>
    [Fact]
    public async Task INT_GEN_006_AC3_WithNoFileAvailableNoLocationIsAnsweredAsync()
    {
        Result<SessionLocation?> where = await Database.ResolveAsync(
            "198.51.100.7",
            TestContext.Current.CancellationToken);

        Assert.Null(where.Match(
            place => place,
            error => throw new Xunit.Sdk.XunitException($"The resolve was refused: {error.Code}.")));
    }

    /// <summary>
    /// INT-GEN-006 AC2: the missing file surfaces as the degradation condition, under
    /// a scope of its own so that the router carries it once a window and not once a
    /// sign-in, and not under another degradation's key.
    /// </summary>
    [Fact]
    public async Task INT_GEN_006_AC2_TheMissingFileSurfacesAsADegradationAsync()
    {
        for (int session = 0; session < 3; session++)
        {
            _ = await Database.ResolveAsync(
                "198.51.100.7",
                TestContext.Current.CancellationToken);
        }

        Assert.Equal(3, _events.Of<AlertRaised>().Count);
        Assert.All(
            _events.Of<AlertRaised>(),
            raised =>
            {
                Assert.Equal(AlertCondition.Degradation, raised.Condition);
                Assert.Equal(
                    Alerts.Key(AlertCondition.Degradation, "location.database.absent"),
                    Alerts.Deduplication(raised.IdempotencyKey));
            });
    }
}
