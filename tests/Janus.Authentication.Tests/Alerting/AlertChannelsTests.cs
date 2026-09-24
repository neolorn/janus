using System;
using System.Threading.Tasks;
using Janus.Authentication.Alerting;
using Janus.Core;
using Xunit;

namespace Janus.Authentication.Tests.Alerting;

/// <summary>
/// The one path a raised condition takes: written for the alert channels in the
/// transaction that raised it, and announced to the host (OPS-ALERT-001,
/// CONV-DESIGN-002).
/// </summary>
[Trait("kind", "unit")]
public sealed class AlertChannelsTests : IAsyncDisposable
{
    private static readonly DateTimeOffset Noon = new(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly RaisedAlertsInMemory _raised = new();
    private readonly EventsInMemory _events = new();
    private readonly UnitOfWorkInMemory _work = new();

    private AlertChannels Channels => new(_raised, _events, _work);

    /// <inheritdoc/>
    public async ValueTask DisposeAsync() => await _work.DisposeAsync();

    /// <summary>
    /// OPS-ALERT-001 AC1: a raised condition waits for the channels and reaches the host
    /// as the event, both in the one transaction.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task OPS_ALERT_001_AC1_ARaisedConditionWaitsForTheChannelsAsync()
    {
        AlertRaised raised = Alerts.Of(AlertCondition.BreakGlassUsed, "issue", Noon);

        Result outcome = await Channels.RaiseAsync(raised, TestContext.Current.CancellationToken);

        Assert.Null(outcome.Match(() => (Error?)null, error => error));
        Assert.Same(raised, Assert.Single(_raised.Waiting).Raised);
        Assert.Same(raised, Assert.Single(_events.Published));
        Assert.Equal(1, _work.Committed);
    }

    /// <summary>
    /// CONV-DESIGN-005 AC1: a host that does not take the event fails the raise, which
    /// commits nothing.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task CONV_DESIGN_005_AC1_AnEventTheHostRefusedFailsTheRaiseAsync()
    {
        _events.Refusal = Error.From(ErrorCodes.SystemFault);

        Result outcome = await Channels.RaiseAsync(
            Alerts.Of(AlertCondition.Degradation, null, Noon),
            TestContext.Current.CancellationToken);

        Assert.Equal(ErrorCodes.SystemFault, outcome.Match(() => (Error?)null, error => error)?.Code);
        Assert.Equal(0, _work.Committed);
    }
}
