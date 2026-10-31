using System;
using System.Text.Json;
using System.Threading.Tasks;
using Janus.Authentication.Alerting;
using Janus.Authentication.Maintenance;
using Janus.Core;
using Janus.Core.Configuration;
using Xunit;

namespace Janus.Authentication.Tests.Maintenance;

/// <summary>
/// The daily look at the key-encryption key's cryptoperiod, warned of from the key's own
/// rotation record (DR-009a, D-166 341, D-225).
/// </summary>
[Trait("kind", "unit")]
public sealed class CryptoperiodWatchTests
{
    private static readonly AccessContext Watcher = AccessContext.Of(
        SystemPrincipal.ForDeployment(CryptoperiodWatch.Job, "DR-009a", SystemOperation.Monitoring));

    private static readonly DateTimeOffset Noon = new(2026, 9, 24, 12, 0, 0, TimeSpan.Zero);

    private const string KeyEncryptionKey = "key-encryption-key";

    private readonly CryptoperiodStoreInMemory _store = new();
    private readonly ConfigurationInMemory _configuration = new();
    private readonly EventsInMemory _alerts = new();
    private readonly FixedClock _clock = new(Noon);

    private CryptoperiodWatch Watch => new(_store, _configuration, _alerts, _clock);

    /// <summary>
    /// DR-009a AC1 and AC6: inside <c>maintenance.expiry.warninglead</c> of the end of the
    /// year since the last rotation, the cryptoperiod is raised as <c>expiry-approaching</c>
    /// under <c>kek-cryptoperiod</c>, naming the version, when it was rotated in and when
    /// its cryptoperiod ends.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task DR_009a_AC1_TheCryptoperiodInsideTheLeadIsRaisedAsync()
    {
        DateTimeOffset rotated = Noon.AddYears(-1).AddDays(-3);

        _store.Rotations.Add((KeyEncryptionKey, 2, rotated));

        Assert.True(await RaisedAsync());

        AlertRaised raised = Assert.Single(_alerts.Of<AlertRaised>());

        Assert.Equal(AlertCondition.ExpiryApproaching, raised.Condition);
        Assert.Equal(CryptoperiodWatch.Job, raised.Scope);
        Assert.StartsWith(
            Alerts.Key(AlertCondition.ExpiryApproaching, "kek-cryptoperiod", named: null) + "@",
            raised.IdempotencyKey,
            StringComparison.Ordinal);
        Assert.Equal(2, raised.Details["version"].GetInt32());
        Assert.Equal(rotated, raised.Details["rotatedAt"].GetDateTimeOffset());
        Assert.Equal(rotated.AddYears(1), raised.Details["dueAt"].GetDateTimeOffset());
    }

    /// <summary>
    /// DR-009a AC1: a rotation within the year and outside the lead of its end raises
    /// nothing.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task DR_009a_AC1_ARotationWithinItsCryptoperiodRaisesNothingAsync()
    {
        _store.Rotations.Add((KeyEncryptionKey, 2, Noon.AddDays(-100)));

        Assert.False(await RaisedAsync());
        Assert.Empty(_alerts.Of<AlertRaised>());
    }

    /// <summary>
    /// DR-009a AC1: the lead is read from <c>maintenance.expiry.warninglead</c>.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task DR_009a_AC1_TheLeadIsTheConfiguredOneAsync()
    {
        _store.Rotations.Add((KeyEncryptionKey, 2, Noon.AddYears(-1).AddDays(45)));

        Assert.False(await RaisedAsync());

        _configuration.Set(Settings.MaintenanceExpiryWarningLead, TimeSpan.FromDays(60));

        Assert.True(await RaisedAsync());
    }

    /// <summary>
    /// DR-009a AC1: the warning depends on no one remembering, so it goes on being raised
    /// at every look, and a completed rotation of the key-encryption key ends it.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task DR_009a_AC1_ACompletedRotationEndsTheWarningAsync()
    {
        _store.Rotations.Add((KeyEncryptionKey, 2, Noon.AddYears(-1).AddDays(29)));

        Assert.True(await RaisedAsync());
        Assert.True(await RaisedAsync());

        _store.Rotations.Add((KeyEncryptionKey, 3, Noon.AddDays(-1)));

        Assert.False(await RaisedAsync());
        Assert.Equal(2, _alerts.Of<AlertRaised>().Count);
    }

    /// <summary>
    /// DR-009a AC1: a deployment whose key-encryption key was never rotated measures the
    /// cryptoperiod from bootstrap, with no version named, and raises nothing before the
    /// lead; a deployment never bootstrapped has no key to warn of.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task DR_009a_AC1_ADeploymentNeverRotatedCountsFromBootstrapAsync()
    {
        Assert.False(await RaisedAsync());

        _store.Bootstrapped = Noon.AddDays(-100);

        Assert.False(await RaisedAsync());

        DateTimeOffset bootstrapped = Noon.AddYears(-1).AddDays(10);

        _store.Bootstrapped = bootstrapped;

        Assert.True(await RaisedAsync());

        AlertRaised raised = Assert.Single(_alerts.Of<AlertRaised>());

        Assert.Equal(JsonValueKind.Null, raised.Details["version"].ValueKind);
        Assert.Equal(bootstrapped, raised.Details["rotatedAt"].GetDateTimeOffset());
        Assert.Equal(bootstrapped.AddYears(1), raised.Details["dueAt"].GetDateTimeOffset());
    }

    /// <summary>
    /// DR-009a AC6: a rotation of the fingerprint key completed since does not end the
    /// warning of the key-encryption key's cryptoperiod.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task DR_009a_AC6_AFingerprintKeyRotationDoesNotEndTheWarningAsync()
    {
        _store.Bootstrapped = Noon.AddYears(-2);
        _store.Rotations.Add((KeyEncryptionKey, 2, Noon.AddYears(-1).AddDays(-3)));
        _store.Rotations.Add(("fingerprint-key", 2, Noon.AddDays(-1)));

        Assert.True(await RaisedAsync());
        Assert.Equal(2, Assert.Single(_alerts.Of<AlertRaised>()).Details["version"].GetInt32());
    }

    private async Task<bool> RaisedAsync() =>
        (await Watch.WatchAsync(Watcher, TestContext.Current.CancellationToken)).Match(
            raised => raised,
            error => throw new Xunit.Sdk.XunitException(error.Code.ToString()));
}
