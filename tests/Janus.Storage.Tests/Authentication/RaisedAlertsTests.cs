using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Dapper;
using Janus.Authentication.Alerting;
using Janus.Core;
using Janus.Storage.Authentication.Alerting;
using Npgsql;
using Xunit;

namespace Janus.Storage.Tests.Authentication;

/// <summary>
/// What waits between the transaction that raised a condition and the alert channels
/// that carry it (OPS-ALERT-001, CONV-DESIGN-002).
/// </summary>
/// <remarks>
/// One database serves the class and the channels read every waiting row, so each
/// test begins by emptying the table.
/// </remarks>
[Trait("kind", "integration")]
public sealed class RaisedAlertsTests(DatabaseFixture database) : IClassFixture<DatabaseFixture>
{
    private static readonly DateTimeOffset Noon = new(2026, 9, 24, 12, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// OPS-ALERT-001 AC1: a raised condition reads back as it was raised, its severity
    /// and its details included, and the oldest is read first.
    /// </summary>
    [Fact]
    public async Task OPS_ALERT_001_AC1_ARaisedConditionReadsBackAsItWasRaisedAsync()
    {
        await EmptiedAsync();

        AlertRaised later = Alerts.Of(AlertCondition.RestrictionGranted, "later", Noon.AddMinutes(1));
        AlertRaised earlier = Alerts.Of(
            AlertCondition.BreakGlassUsed,
            "earlier",
            Noon,
            new Dictionary<string, JsonElement>(StringComparer.Ordinal)
            {
                ["event"] = JsonSerializer.SerializeToElement("generated"),
                ["remaining"] = JsonSerializer.SerializeToElement(4),
            });

        await WrittenAsync(later);
        await WrittenAsync(earlier);

        await using StoreContext reading = database.Context();

        IReadOnlyList<RaisedAlert> waiting = await new RaisedAlerts(reading)
            .OldestAsync(10, TestContext.Current.CancellationToken);

        Assert.Equal([earlier.IdempotencyKey, later.IdempotencyKey], waiting.Select(alert => alert.Raised.IdempotencyKey));

        AlertRaised held = waiting[0].Raised;

        Assert.Equal(Noon, held.RaisedAt);
        Assert.Equal(AlertCondition.BreakGlassUsed, held.Condition);
        Assert.Equal(earlier.Severity, held.Severity);
        Assert.Equal("generated", held.Details["event"].GetString());
        Assert.Equal(4, held.Details["remaining"].GetInt32());
        Assert.Empty(waiting[1].Raised.Details);
    }

    /// <summary>
    /// OPS-ALERT-001 AC1: a carried condition is removed, so it is carried once, and the
    /// read takes no more than it was asked for.
    /// </summary>
    [Fact]
    public async Task OPS_ALERT_001_AC1_ACarriedConditionIsRemovedAsync()
    {
        await EmptiedAsync();

        RaisedAlertId carried = await WrittenAsync(Alerts.Of(AlertCondition.Degradation, null, Noon));
        RaisedAlertId waiting = await WrittenAsync(Alerts.Of(AlertCondition.Degradation, null, Noon.AddMinutes(1)));

        await using (StoreContext writing = database.Context())
        {
            RaisedAlerts alerts = new(writing);

            Assert.Equal(
                carried,
                Assert.Single(await alerts.OldestAsync(1, TestContext.Current.CancellationToken)).Id);

            await alerts.RemoveAsync(carried, TestContext.Current.CancellationToken);
            await writing.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using StoreContext reading = database.Context();

        Assert.Equal(
            waiting,
            Assert.Single(await new RaisedAlerts(reading).OldestAsync(10, TestContext.Current.CancellationToken)).Id);
    }

    private async Task<RaisedAlertId> WrittenAsync(AlertRaised raised)
    {
        var alert = new RaisedAlert(RaisedAlertId.Of(raised.RaisedAt), raised);

        await using StoreContext writing = database.Context();

        await new RaisedAlerts(writing).AddAsync(alert, TestContext.Current.CancellationToken);
        await writing.SaveChangesAsync(TestContext.Current.CancellationToken);

        return alert.Id;
    }

    private async Task EmptiedAsync()
    {
        await using NpgsqlConnection connection = await database.OpenAsync();

        await connection.ExecuteAsync("DELETE FROM identity.raised_alerts;");
    }
}
