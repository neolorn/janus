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
/// The daily look at the key-encryption key's cryptoperiod: the annual operation it is
/// rotated in, warned of from the maintenance log before it falls due and for as long as
/// it goes undone (DR-009a, OPS-MAINT-001, entry 341).
/// </summary>
[Trait("kind", "unit")]
public sealed class EnvelopeRotationWatchTests
{
    private static readonly DateTimeOffset Noon = new(2026, 9, 24, 12, 0, 0, TimeSpan.Zero);

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
            Alerts.Key(AlertCondition.ExpiryApproaching, "envelope-rotation") + "@",
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

    private async Task RecordedAsync(MaintenanceTask task, DateTimeOffset performedAt) =>
        await _store.RecordAsync(
            new MaintenanceEntry(MaintenanceEntryId.Of(performedAt), task, performedAt, Owner, Note: null),
            TestContext.Current.CancellationToken);

    private async Task<bool> RaisedAsync() =>
        (await Watch.WatchAsync(TestContext.Current.CancellationToken)).Match(
            raised => raised,
            error => throw new Xunit.Sdk.XunitException(error.Code.ToString()));
}
