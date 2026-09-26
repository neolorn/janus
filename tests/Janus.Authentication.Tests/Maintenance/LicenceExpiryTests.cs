using System;
using System.Linq;
using System.Threading.Tasks;
using Janus.Authentication.Alerting;
using Janus.Authentication.Maintenance;
using Janus.Core;
using Janus.Core.Configuration;
using Xunit;

namespace Janus.Authentication.Tests.Maintenance;

/// <summary>
/// The daily warning of a licence or permit about to lapse (OPS-MAINT-001 AC2,
/// OPS-ALERT-001).
/// </summary>
[Trait("kind", "unit")]
public sealed class LicenceExpiryTests
{
    private static readonly DateTimeOffset Noon = new(2026, 9, 24, 12, 0, 0, TimeSpan.Zero);

    private readonly MaintenanceStoreInMemory _store = new();
    private readonly ConfigurationInMemory _configuration = new();
    private readonly EventsInMemory _alerts = new();
    private readonly FixedClock _clock = new(Noon);

    private LicenceExpiry Expiry => new(_store, _configuration, _alerts, _clock);

    /// <summary>
    /// OPS-MAINT-001 AC2: a licence whose expiry is inside
    /// <c>maintenance.expiry.warninglead</c> (30 days by default) raises
    /// <c>expiry-approaching</c> under its own identifier, and one further off raises
    /// nothing.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task OPS_MAINT_001_AC2_ALicenceInsideTheLeadRaisesExpiryApproachingAsync()
    {
        Licence near = Licence("Premises permit", Noon.AddDays(29), LicenceKind.Permit);
        Licence far = Licence("Operating licence", Noon.AddDays(31), LicenceKind.Licence);

        await _store.ReplaceLicencesAsync([near, far], TestContext.Current.CancellationToken);

        Assert.Equal(1, await WarnedAsync());

        AlertRaised raised = Assert.Single(_alerts.Of<AlertRaised>());

        Assert.Equal(AlertCondition.ExpiryApproaching, raised.Condition);
        Assert.StartsWith(
            Alerts.Key(AlertCondition.ExpiryApproaching, "licence:" + near.Id) + "@",
            raised.IdempotencyKey,
            StringComparison.Ordinal);
        Assert.Equal(near.Id.ToString(), raised.Details["licence"].GetString());
        Assert.Equal("permit", raised.Details["kind"].GetString());
        Assert.Equal("Premises permit", raised.Details["name"].GetString());
        Assert.Equal(near.ExpiresAt, raised.Details["expiresAt"].GetDateTimeOffset());
    }

    /// <summary>
    /// OPS-MAINT-001 AC2: the warning does not depend on anyone remembering, so a
    /// licence that lapsed unrenewed is still raised at the next pass.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task OPS_MAINT_001_AC2_ALicenceThatLapsedUnrenewedIsStillRaisedAsync()
    {
        Licence lapsed = Licence("Operating licence", Noon.AddDays(-3), LicenceKind.Licence);

        await _store.ReplaceLicencesAsync([lapsed], TestContext.Current.CancellationToken);

        Assert.Equal(1, await WarnedAsync());
        Assert.Equal(AlertCondition.ExpiryApproaching, Assert.Single(_alerts.Of<AlertRaised>()).Condition);
    }

    /// <summary>
    /// OPS-MAINT-001 AC2: the lead is read from <c>maintenance.expiry.warninglead</c>,
    /// so a deployment that wants longer notice is warned sooner.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task OPS_MAINT_001_AC2_TheLeadIsTheConfiguredOneAsync()
    {
        _configuration.Set(Settings.MaintenanceExpiryWarningLead, TimeSpan.FromDays(60));

        await _store.ReplaceLicencesAsync(
            [Licence("Operating licence", Noon.AddDays(45), LicenceKind.Licence)],
            TestContext.Current.CancellationToken);

        Assert.Equal(1, await WarnedAsync());
        Assert.Equal(
            [AlertCondition.ExpiryApproaching],
            _alerts.Of<AlertRaised>().Select(raised => raised.Condition));
    }

    private static Licence Licence(string name, DateTimeOffset expiresAt, LicenceKind kind) =>
        new(new LicenceId(Guid.CreateVersion7(expiresAt)), kind, name, expiresAt, RenewedAt: null);

    private async Task<int> WarnedAsync() =>
        (await Expiry.WarnAsync(TestContext.Current.CancellationToken)).Match(
            warned => warned,
            error => throw new Xunit.Sdk.XunitException(error.Code.ToString()));
}
