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

    private static readonly TimeSpan Timeout = TimeSpan.FromMinutes(2);

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
            .OldestAsync(Noon.AddDays(1), 10, TestContext.Current.CancellationToken);

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
    /// OPS-ALERT-001 AC1, CONV-DESIGN-003 AC9: a condition is claimed before it is
    /// carried and removed under that claim, so it is carried once, and the read takes
    /// no more than it was asked for.
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
                Assert.Single(await alerts.OldestAsync(Noon, 1, TestContext.Current.CancellationToken)).Id);

            DateTimeOffset claim = await alerts.ClaimAsync(carried, Noon, Timeout, TestContext.Current.CancellationToken)
                ?? throw new Xunit.Sdk.XunitException("The condition was not claimed.");

            Assert.True(await alerts.RemoveAsync(carried, claim, TestContext.Current.CancellationToken));
        }

        await using StoreContext reading = database.Context();

        Assert.Equal(
            waiting,
            Assert.Single(await new RaisedAlerts(reading).OldestAsync(Noon, 10, TestContext.Current.CancellationToken)).Id);
    }

    /// <summary>
    /// CONV-DESIGN-003 AC9, INF-BG-001 AC4, OPS-ALERT-002 AC2: of several passes that
    /// reach one raised condition at once, one claims it. While the claim stands no pass
    /// reads the row or takes it; a claim given up, or one that has timed out, lets the
    /// next pass take it; and an outcome whose claim was taken over changes nothing.
    /// </summary>
    [Fact]
    public async Task CONV_DESIGN_003_AC9_ARaisedConditionIsClaimedByOnePassAsync()
    {
        await EmptiedAsync();

        RaisedAlertId raised = await WrittenAsync(Alerts.Of(AlertCondition.Degradation, null, Noon));

        DateTimeOffset?[] claims = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => ClaimedAsync(raised, Noon)));

        DateTimeOffset claim = Assert.Single(claims, one => one is not null)!.Value;

        Assert.Equal(Noon + Timeout, claim);
        Assert.Null(await ClaimedAsync(raised, Noon + Timeout - TimeSpan.FromSeconds(1)));

        await using (StoreContext reading = database.Context())
        {
            Assert.Empty(await new RaisedAlerts(reading).OldestAsync(Noon.AddSeconds(1), 10, TestContext.Current.CancellationToken));
            Assert.Single(await new RaisedAlerts(reading).OldestAsync(Noon + Timeout, 10, TestContext.Current.CancellationToken));
        }

        DateTimeOffset taken = await ClaimedAsync(raised, Noon + Timeout)
            ?? throw new Xunit.Sdk.XunitException("The claim was not taken over.");

        await using (StoreContext late = database.Context())
        {
            Assert.False(await new RaisedAlerts(late).RemoveAsync(raised, claim, TestContext.Current.CancellationToken));
            Assert.False(await new RaisedAlerts(late).ReleaseAsync(raised, claim, TestContext.Current.CancellationToken));
            Assert.True(await new RaisedAlerts(late).ReleaseAsync(raised, taken, TestContext.Current.CancellationToken));
        }

        await using StoreContext after = database.Context();

        Assert.Single(await new RaisedAlerts(after).OldestAsync(Noon + Timeout, 10, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// OPS-ALERT-002 AC3, D-177: a condition raised under a scope reads back with that
    /// scope, and one raised under none reads back with none.
    /// </summary>
    [Fact]
    public async Task OPS_ALERT_002_AC3_ARaisedConditionReadsBackWithItsScopeAsync()
    {
        await EmptiedAsync();

        AlertRaised scoped = Alerts.Scoped(AlertCondition.Degradation, "mailbox.reconciliation", Noon);
        AlertRaised named = Alerts.Of(AlertCondition.RestrictionGranted, "someone", Noon.AddMinutes(1));

        await WrittenAsync(scoped);
        await WrittenAsync(named);

        await using StoreContext reading = database.Context();

        IReadOnlyList<RaisedAlert> waiting = await new RaisedAlerts(reading)
            .OldestAsync(Noon.AddDays(1), 10, TestContext.Current.CancellationToken);

        Assert.Equal("mailbox.reconciliation", waiting[0].Raised.Scope);
        Assert.Equal(scoped.IdempotencyKey, waiting[0].Raised.IdempotencyKey);
        Assert.Null(waiting[1].Raised.Scope);
    }

    // A claim as a pass takes it: one conditional update, committed on its own.
    private async Task<DateTimeOffset?> ClaimedAsync(RaisedAlertId alert, DateTimeOffset now)
    {
        await using StoreContext claiming = database.Context();

        return await new RaisedAlerts(claiming).ClaimAsync(alert, now, Timeout, TestContext.Current.CancellationToken);
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
