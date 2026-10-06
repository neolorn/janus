using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Alerting;
using Janus.Core;
using Janus.Core.Configuration;
using Xunit;

namespace Janus.Hosting.Tests.Alerting;

/// <summary>
/// The alert channels carrying what committed transactions raised: by the router, to
/// the destinations, oldest first, each once (OPS-ALERT-001, CONV-DESIGN-002).
/// </summary>
[Trait("kind", "unit")]
public sealed class AlertDispatchTests : IAsyncDisposable
{
    private const string Operator = "ops@example.test";

    private readonly Deployment _deployment = new();

    /// <summary>
    /// A deployment that names its operator's destinations and not yet its owner's.
    /// </summary>
    public AlertDispatchTests()
    {
        Flow.Prepare(_deployment);

        _deployment.Configuration.Set(Settings.AlertingEmailDestinations, [Operator]);
        _deployment.Configuration.Set(Settings.AlertingSmsDestinations, ["+441632960098"]);
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync() => await _deployment.DisposeAsync();

    /// <summary>
    /// OPS-ALERT-001 AC1: a raised condition reaches the operator with nobody watching,
    /// once, and leaves nothing waiting.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task OPS_ALERT_001_AC1_ARaisedConditionIsCarriedOnceAsync()
    {
        Owned();

        await RaisedAsync(AlertCondition.RestrictionGranted, "first");
        await RaisedAsync(AlertCondition.RestrictionLoosened, "second");

        Assert.Equal(2, await _deployment.CarryAlertsAsync());
        Assert.Equal(0, await _deployment.CarryAlertsAsync());
        Assert.Empty(_deployment.Raised.Waiting);
        Assert.Equal(
            [Operator, Operator],
            _deployment.Mail.Taken.Select(mail => mail.Destination.Value));
    }

    /// <summary>
    /// OPS-ALERT-001: a condition the router cannot carry, because the deployment does
    /// not yet name every destination, waits for a later pass rather than being lost.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task OPS_ALERT_001_AConditionTheRouterRefusedWaitsAsync()
    {
        await RaisedAsync(AlertCondition.BreakGlassUsed, "issue");

        _ = await Assert.ThrowsAsync<InvalidOperationException>(_deployment.CarryAlertsAsync);

        Assert.Single(_deployment.Raised.Waiting);
        Assert.Empty(_deployment.Mail.Taken);

        Owned();

        Assert.Equal(1, await _deployment.CarryAlertsAsync());
        Assert.Empty(_deployment.Raised.Waiting);
    }

    /// <summary>
    /// OPS-ALERT-002, D-022: a pass commits each condition's deduplication claim before
    /// the router carries it, so no transport is called while a transaction is open.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task OPS_ALERT_002_NoAlertIsSentWhileATransactionIsOpenAsync()
    {
        Owned();

        var open = new List<int>();

        _deployment.Mail.Handed = () => open.Add(_deployment.Work.Opened - _deployment.Work.Committed);

        await RaisedAsync(AlertCondition.RestrictionGranted, "first");

        Assert.Equal(1, await _deployment.CarryAlertsAsync());
        Assert.Equal([0], open);
        Assert.Empty(_deployment.Raised.Waiting);
    }

    /// <summary>
    /// CONV-DESIGN-003 AC9, INF-BG-001 AC4: a raised condition another pass holds is
    /// that pass's: this one carries nothing of it and leaves the row as it stands, and
    /// once that claim has timed out the next pass carries it. A condition the router
    /// refused gives its claim up, so the next pass takes it without waiting.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task CONV_DESIGN_003_AC9_AConditionAnotherPassHoldsIsNotCarriedAsync()
    {
        Owned();

        await RaisedAsync(AlertCondition.RestrictionGranted, "first");

        RaisedAlert held = Assert.Single(_deployment.Raised.Waiting);
        TimeSpan timeout = Settings.OutboxClaimTimeout.Default;

        _deployment.Raised.TakeOver(held.Id, _deployment.Clock.GetUtcNow() + timeout);

        Assert.Equal(0, await _deployment.CarryAlertsAsync());
        Assert.Empty(_deployment.Mail.Taken);
        Assert.Single(_deployment.Raised.Waiting);

        _deployment.Clock.Advance(timeout);

        Assert.Equal(1, await _deployment.CarryAlertsAsync());
        Assert.Single(_deployment.Mail.Taken);
        Assert.Empty(_deployment.Raised.Waiting);
    }

    private void Owned()
    {
        _deployment.Configuration.Set(Settings.AlertingOwnerEmail, "owner@example.test");
        _deployment.Configuration.Set(Settings.AlertingOwnerSms, "+441632960099");
    }

    private async Task RaisedAsync(AlertCondition condition, string scope)
    {
        AlertRaised raised = Alerts.Of(condition, scope, _deployment.Clock.GetUtcNow());

        await _deployment.Raised.AddAsync(new RaisedAlert(RaisedAlertId.Of(raised.RaisedAt), raised), CancellationToken.None);
    }
}
