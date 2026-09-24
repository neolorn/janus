using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Alerting;
using Janus.Core;
using Janus.Core.Configuration;
using Xunit;

namespace Janus.Authentication.Tests.Alerting;

/// <summary>
/// What the environment's clock and certificate renewal raise, read from what the
/// deployment registers over them (INF-HOST-001, INF-TLS-003, OPS-ALERT-001).
/// </summary>
[Trait("kind", "unit")]
public sealed class EnvironmentWatchTests
{
    private static readonly DateTimeOffset Noon = new(2026, 9, 24, 12, 0, 0, TimeSpan.Zero);

    private readonly EnvironmentInMemory _environment = new();
    private readonly ConfigurationInMemory _configuration = new();
    private readonly EventsInMemory _alerts = new();
    private readonly FixedClock _clock = new(Noon);

    private ClockDriftWatch Clock => new(_environment, _configuration, _alerts, _clock);

    private CertificateRenewalWatch Renewal => new(_environment, _alerts, _clock);

    /// <summary>
    /// INF-HOST-001 AC2, OPS-ALERT-001 AC1: a clock further out than a code is accepted
    /// from, either way, raises <c>clock-drift</c> at Normal with nobody watching,
    /// naming the offset and the tolerance.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task INF_HOST_001_AC2_DriftBeyondToleranceRaisesAnAlertAsync()
    {
        _environment.Measured(TimeSpan.FromSeconds(31));
        await WatchedAsync(Clock.WatchAsync);

        _environment.Measured(TimeSpan.FromSeconds(-45));
        await WatchedAsync(Clock.WatchAsync);

        AlertRaised[] raised = [.. _alerts.Of<AlertRaised>()];

        Assert.Equal(2, raised.Length);
        Assert.All(raised, alert =>
        {
            Assert.Equal(AlertCondition.ClockDrift, alert.Condition);
            Assert.Equal(AlertSeverity.Normal, alert.Severity);
            Assert.Equal(Alerts.Key(AlertCondition.ClockDrift, scope: null), Alerts.Deduplication(alert.IdempotencyKey));
            Assert.Equal(30d, alert.Details["toleranceSeconds"].GetDouble());
        });
        Assert.Equal(31d, raised[0].Details["offsetSeconds"].GetDouble());
        Assert.Equal(-45d, raised[1].Details["offsetSeconds"].GetDouble());
    }

    /// <summary>
    /// INF-HOST-001 AC2: a clock within the tolerance raises nothing, its edge
    /// included.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task INF_HOST_001_DriftWithinToleranceRaisesNothingAsync()
    {
        foreach (TimeSpan offset in new[] { TimeSpan.Zero, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(-30) })
        {
            _environment.Measured(offset);
            await WatchedAsync(Clock.WatchAsync);
        }

        Assert.Empty(_alerts.Of<AlertRaised>());
    }

    /// <summary>
    /// INF-HOST-001: the tolerance is the one a time-based code is accepted within,
    /// so it follows <c>factor.totp.drift</c>.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task INF_HOST_001_TheToleranceFollowsTheCodeDriftAsync()
    {
        _configuration.Set(Settings.FactorTotpDrift, 2);

        _environment.Measured(TimeSpan.FromSeconds(45));
        await WatchedAsync(Clock.WatchAsync);

        Assert.Empty(_alerts.Of<AlertRaised>());

        _environment.Measured(TimeSpan.FromSeconds(61));
        await WatchedAsync(Clock.WatchAsync);

        AlertRaised raised = Assert.Single(_alerts.Of<AlertRaised>());

        Assert.Equal(60d, raised.Details["toleranceSeconds"].GetDouble());
    }

    /// <summary>
    /// INF-HOST-001 AC2: a clock nothing measures is not known to be within the
    /// tolerance, so a deployment that registered no reference, or one that holds no
    /// measurement, is raised as a degradation.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task INF_HOST_001_AC2_AnUnmeasuredClockIsRaisedAsADegradationAsync()
    {
        await WatchedAsync(new ClockDriftWatch(reference: null, _configuration, _alerts, _clock).WatchAsync);

        _environment.Unmeasured();
        await WatchedAsync(Clock.WatchAsync);

        Assert.Equal(
            [
                Alerts.Key(AlertCondition.Degradation, "clock.reference.absent"),
                Alerts.Key(AlertCondition.Degradation, "clock.reference.unread"),
            ],
            _alerts.Of<AlertRaised>().Select(alert => Alerts.Deduplication(alert.IdempotencyKey)));
    }

    /// <summary>
    /// INF-TLS-003 AC2, OPS-ALERT-001 AC1: a failed renewal raises
    /// <c>certificate-renewal-failed</c> at High with nobody checking, naming when it
    /// failed, and goes on being raised until a renewal succeeds.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task INF_TLS_003_AC2_ARenewalFailureRaisesAnAlertWithoutAnyoneCheckingAsync()
    {
        DateTimeOffset failedAt = Noon.AddMinutes(-20);

        _environment.Renewed(failedAt);
        await WatchedAsync(Renewal.WatchAsync);

        AlertRaised raised = Assert.Single(_alerts.Of<AlertRaised>());

        Assert.Equal(AlertCondition.CertificateRenewalFailed, raised.Condition);
        Assert.Equal(AlertSeverity.High, raised.Severity);
        Assert.Equal(failedAt, raised.Details["failedAt"].GetDateTimeOffset());

        _clock.Advance(TimeSpan.FromHours(1));
        await WatchedAsync(Renewal.WatchAsync);

        Assert.Equal(2, _alerts.Of<AlertRaised>().Count);

        _environment.Renewed(failedAt: null);
        _clock.Advance(TimeSpan.FromHours(1));
        await WatchedAsync(Renewal.WatchAsync);

        Assert.Equal(2, _alerts.Of<AlertRaised>().Count);
    }

    /// <summary>
    /// INF-TLS-003 AC2: a renewal nothing watches is not known to work, so a deployment
    /// that registered nothing over its renewer, or one whose outcome cannot be read, is
    /// raised as a degradation.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task INF_TLS_003_AC2_AnUnwatchedRenewalIsRaisedAsADegradationAsync()
    {
        await WatchedAsync(new CertificateRenewalWatch(renewal: null, _alerts, _clock).WatchAsync);

        _environment.RenewalUnread();
        await WatchedAsync(Renewal.WatchAsync);

        Assert.Equal(
            [
                Alerts.Key(AlertCondition.Degradation, "certificate.renewal.absent"),
                Alerts.Key(AlertCondition.Degradation, "certificate.renewal.unread"),
            ],
            _alerts.Of<AlertRaised>().Select(alert => Alerts.Deduplication(alert.IdempotencyKey)));
    }

    private static async Task WatchedAsync(Func<CancellationToken, ValueTask<Result>> watch) =>
        Assert.Null((await watch(TestContext.Current.CancellationToken)).Match(() => (Error?)null, error => error));
}
