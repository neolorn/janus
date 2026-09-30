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
/// The daily look at the annual envelope operation, warned of from the maintenance log
/// before it falls due and for as long as it goes undone, and at the key-encryption key's
/// cryptoperiod, warned of from the key's own rotation record (DR-009a, OPS-MAINT-001,
/// D-166 341).
/// </summary>
[Trait("kind", "unit")]
public sealed class EnvelopeRotationWatchTests
{
    private static readonly DateTimeOffset Noon = new(2026, 9, 24, 12, 0, 0, TimeSpan.Zero);

    private const string Cryptoperiod = "kek-cryptoperiod";

    private const string KeyEncryptionKey = "key-encryption-key";

    private static readonly SubjectId Owner = new(Guid.CreateVersion7(Noon));

    private readonly MaintenanceStoreInMemory _store = new();
    private readonly ConfigurationInMemory _configuration = new();
    private readonly EventsInMemory _alerts = new();
    private readonly FixedClock _clock = new(Noon);

    private EnvelopeRotationWatch Watch => new(_store, _configuration, _alerts, _clock);

    /// <summary>
    /// DR-009a AC1: the cryptoperiod is a year from the last operation the log records,
    /// and inside <c>maintenance.expiry.warninglead</c> of its end the operation is raised
    /// as <c>expiry-approaching</c>, naming when it was performed and when it is due.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task DR_009a_AC1_TheOperationInsideTheLeadIsRaisedAsDueAsync()
    {
        DateTimeOffset performed = Noon.AddYears(-1).AddDays(29);

        await RecordedAsync(MaintenanceTask.EnvelopeRotation, performed);

        Assert.True(await RaisedAsync());

        AlertRaised raised = Assert.Single(_alerts.Of<AlertRaised>());

        Assert.Equal(AlertCondition.ExpiryApproaching, raised.Condition);
        Assert.StartsWith(
            Alerts.Key(AlertCondition.ExpiryApproaching, "envelope-rotation", named: null) + "@",
            raised.IdempotencyKey,
            StringComparison.Ordinal);
        Assert.Equal("envelope-rotation", raised.Details["task"].GetString());
        Assert.Equal(performed, raised.Details["performedAt"].GetDateTimeOffset());
        Assert.Equal(performed.AddYears(1), raised.Details["dueAt"].GetDateTimeOffset());
    }

    /// <summary>
    /// DR-009a AC1: an operation performed within the year and outside the lead of its
    /// end raises nothing, whatever other task the log records since, and only the latest
    /// operation counts.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task DR_009a_AC1_AnOperationWithinItsCryptoperiodRaisesNothingAsync()
    {
        await RecordedAsync(MaintenanceTask.EnvelopeRotation, Noon.AddYears(-2));
        await RecordedAsync(MaintenanceTask.EnvelopeRotation, Noon.AddDays(-100));
        await RecordedAsync(MaintenanceTask.LicenceRenewal, Noon.AddDays(-1));

        Assert.False(await RaisedAsync());
        Assert.Empty(_alerts.Of<AlertRaised>());
    }

    /// <summary>
    /// DR-009a AC1: the warning does not depend on anyone remembering, so an operation
    /// left undone past its anniversary goes on being raised, and a log that records none
    /// has it due now.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task DR_009a_AC1_AnOperationUndoneOrNeverRecordedIsRaisedAsync()
    {
        Assert.True(await RaisedAsync());

        AlertRaised never = Assert.Single(_alerts.Of<AlertRaised>());

        Assert.Equal(JsonValueKind.Null, never.Details["performedAt"].ValueKind);

        await RecordedAsync(MaintenanceTask.EnvelopeRotation, Noon.AddYears(-1).AddDays(-3));

        Assert.True(await RaisedAsync());
        Assert.Equal(2, _alerts.Of<AlertRaised>().Count);
    }

    /// <summary>
    /// DR-009a AC1: the lead is read from <c>maintenance.expiry.warninglead</c>, as the
    /// licences' is.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task DR_009a_AC1_TheLeadIsTheConfiguredOneAsync()
    {
        _configuration.Set(Settings.MaintenanceExpiryWarningLead, TimeSpan.FromDays(60));

        await RecordedAsync(MaintenanceTask.EnvelopeRotation, Noon.AddYears(-1).AddDays(45));

        Assert.True(await RaisedAsync());
    }

    /// <summary>
    /// DR-009a AC1, AC6: a maintenance log entry for the envelope operation, with no
    /// rotation of the key-encryption key behind it, leaves the cryptoperiod raised under
    /// <c>kek-cryptoperiod</c>, naming the version, when it was rotated in and when its
    /// cryptoperiod ends.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task DR_009a_AC1_ALogEntryWithoutARotationLeavesTheCryptoperiodRaisedAsync()
    {
        DateTimeOffset rotated = Noon.AddYears(-1).AddDays(-3);

        _store.Rotations.Add((KeyEncryptionKey, 2, rotated));
        await RecordedAsync(MaintenanceTask.EnvelopeRotation, Noon.AddDays(-1));

        Assert.True(await RaisedAsync());

        AlertRaised raised = Assert.Single(_alerts.Of<AlertRaised>());

        Assert.Equal(AlertCondition.ExpiryApproaching, raised.Condition);
        Assert.StartsWith(
            Alerts.Key(AlertCondition.ExpiryApproaching, Cryptoperiod, named: null) + "@",
            raised.IdempotencyKey,
            StringComparison.Ordinal);
        Assert.Equal(2, raised.Details["version"].GetInt32());
        Assert.Equal(rotated, raised.Details["rotatedAt"].GetDateTimeOffset());
        Assert.Equal(rotated.AddYears(1), raised.Details["dueAt"].GetDateTimeOffset());
    }

    /// <summary>
    /// DR-009a AC1: the cryptoperiod is raised from the lead before its end, and a
    /// completed rotation of the key-encryption key ends the warning.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task DR_009a_AC1_ACompletedRotationEndsTheWarningAsync()
    {
        await RecordedAsync(MaintenanceTask.EnvelopeRotation, Noon.AddDays(-1));
        _store.Rotations.Add((KeyEncryptionKey, 2, Noon.AddYears(-1).AddDays(29)));

        Assert.True(await RaisedAsync());

        _store.Rotations.Add((KeyEncryptionKey, 3, Noon.AddDays(-1)));

        Assert.False(await RaisedAsync());
        Assert.Single(_alerts.Of<AlertRaised>());
    }

    /// <summary>
    /// DR-009a AC1: a deployment whose key-encryption key was never rotated measures the
    /// cryptoperiod from bootstrap, with no version named, and raises nothing before the
    /// lead.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task DR_009a_AC1_ADeploymentNeverRotatedCountsFromBootstrapAsync()
    {
        await RecordedAsync(MaintenanceTask.EnvelopeRotation, Noon.AddDays(-1));
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
    public async Task DR_009a_AC1_AFingerprintKeyRotationDoesNotEndTheWarningAsync()
    {
        await RecordedAsync(MaintenanceTask.EnvelopeRotation, Noon.AddDays(-1));
        _store.Bootstrapped = Noon.AddYears(-2);
        _store.Rotations.Add((KeyEncryptionKey, 2, Noon.AddYears(-1).AddDays(-3)));
        _store.Rotations.Add(("fingerprint-key", 2, Noon.AddDays(-1)));

        Assert.True(await RaisedAsync());
        Assert.Equal(2, Assert.Single(_alerts.Of<AlertRaised>()).Details["version"].GetInt32());
    }

    private async Task RecordedAsync(MaintenanceTask task, DateTimeOffset performedAt) =>
        await _store.RecordAsync(
            new MaintenanceEntry(MaintenanceEntryId.Of(performedAt), task, performedAt, Owner, Note: null),
            TestContext.Current.CancellationToken);

    private async Task<bool> RaisedAsync() =>
        (await Watch.WatchAsync(TestContext.Current.CancellationToken)).Match(
            raised => raised,
            error => throw new Xunit.Sdk.XunitException(error.Code.ToString()));
}
