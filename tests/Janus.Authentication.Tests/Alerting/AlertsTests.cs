using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Janus.Authentication.Alerting;
using Janus.Core;
using Xunit;

namespace Janus.Authentication.Tests.Alerting;

/// <summary>
/// The alert table itself: the identifier every condition carries, what each one
/// weighs, and what an alert deduplicates under (OPS-ALERT-001, OPS-ALERT-002,
/// chapter 10 section 5.23).
/// </summary>
[Trait("kind", "unit")]
public sealed class AlertsTests
{
    private static readonly DateTimeOffset Noon = new(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);

    private static readonly string[] Identifiers =
    [
        "auth-failures-sustained",
        "recovery-clustering",
        "approver-volume",
        "read-volume-anomaly",
        "breakglass-used",
        "protected-setting-changed",
        "alert-destination-changed",
        "stepup-policy-weakened",
        "concurrent-sessions-implausible",
        "denial-spike",
        "sms-balance",
        "background-job-failed",
        "erasure-delivery-exhausted",
        "certificate-renewal-failed",
        "clock-drift",
        "degradation",
        "callback-verification-failed",
        "nonexistent-notice-rate",
        "privacy-deadline-approaching",
        "privacy-deadline-reached",
        "expiry-approaching",
        "holiday-list-exhausted",
        "no-emergency-credential",
        "restore-test-failed",
        "restriction-loosened",
        "restriction-granted",
        "domain-reverification-failed",
        "domain-removed",
        "governing-language-changed",
        "governing-text-missing",
        "relay-domain-unregistered",
    ];

    private static readonly AlertCondition[] High =
    [
        AlertCondition.AuthFailuresSustained,
        AlertCondition.RecoveryClustering,
        AlertCondition.ApproverVolume,
        AlertCondition.ReadVolumeAnomaly,
        AlertCondition.BreakGlassUsed,
        AlertCondition.ProtectedSettingChanged,
        AlertCondition.AlertDestinationChanged,
        AlertCondition.StepUpPolicyWeakened,
        AlertCondition.ConcurrentSessionsImplausible,
        AlertCondition.ErasureDeliveryExhausted,
        AlertCondition.CertificateRenewalFailed,
        AlertCondition.PrivacyDeadlineReached,
        AlertCondition.NoEmergencyCredential,
        AlertCondition.RestoreTestFailed,
    ];

    /// <summary>
    /// OPS-ALERT-001 AC2: the conditions are the rows of the table and nothing else,
    /// each carrying the identifier chapter 10 section 5.23 gives it, in table order.
    /// </summary>
    [Fact]
    public void OPS_ALERT_001_AC2_TheConditionsAreTheTableInOrder() =>
        Assert.Equal(
            Identifiers,
            Enum.GetValues<AlertCondition>().Select(condition => Alerts.Key(condition, null)));

    /// <summary>
    /// The severity of a condition is the table's and not the caller's, so two
    /// places raising one condition cannot disagree about it (OPS-ALERT-001).
    /// </summary>
    [Fact]
    public void Severity_EveryCondition_IsTheOneTheTableGivesIt()
    {
        foreach (AlertCondition condition in Enum.GetValues<AlertCondition>())
        {
            Assert.Equal(
                High.Contains(condition) ? AlertSeverity.High : AlertSeverity.Normal,
                Alerts.Severity(condition));
        }
    }

    /// <summary>
    /// OPS-ALERT-002 AC1: an alert deduplicates under the condition identifier and
    /// the account or actor the row names, so two accounts are two alerts.
    /// </summary>
    [Fact]
    public void OPS_ALERT_002_AC1_TheDeduplicationKeyIsTheConditionAndWhoseItIs()
    {
        AlertRaised one = Alerts.Of(AlertCondition.AuthFailuresSustained, "account-one", Noon);
        AlertRaised other = Alerts.Of(
            AlertCondition.AuthFailuresSustained,
            "account-two",
            Noon + TimeSpan.FromMinutes(1));

        Assert.Equal(
            "auth-failures-sustained:account-one",
            Alerts.Deduplication(one.IdempotencyKey));

        Assert.NotEqual(
            Alerts.Deduplication(one.IdempotencyKey),
            Alerts.Deduplication(other.IdempotencyKey));
    }

    /// <summary>
    /// A row that names no one deduplicates under the identifier alone, and the
    /// instant the alert carries is not part of the key (OPS-ALERT-002).
    /// </summary>
    [Fact]
    public void Deduplication_AConditionNamingNoOne_IsTheIdentifierAlone()
    {
        AlertRaised raised = Alerts.Of(AlertCondition.SmsBalance, null, Noon);
        AlertRaised later = Alerts.Of(AlertCondition.SmsBalance, null, Noon + TimeSpan.FromHours(2));

        Assert.Equal("sms-balance", Alerts.Deduplication(raised.IdempotencyKey));
        Assert.Equal(
            Alerts.Deduplication(raised.IdempotencyKey),
            Alerts.Deduplication(later.IdempotencyKey));

        Assert.NotEqual(raised.IdempotencyKey, later.IdempotencyKey);
    }

    /// <summary>
    /// A raised condition carries the instant it fired, its severity and the
    /// structured detail of its row, and nothing a caller decided (OPS-ALERT-001).
    /// </summary>
    [Fact]
    public void Of_ARaisedCondition_CarriesTheRowAndNoWording()
    {
        AlertRaised raised = Alerts.Of(AlertCondition.RestrictionLoosened, "sms.destination", Noon);

        Assert.Equal(Noon, raised.RaisedAt);
        Assert.Equal(AlertCondition.RestrictionLoosened, raised.Condition);
        Assert.Equal(AlertSeverity.Normal, raised.Severity);
        Assert.Empty(raised.Details);
        Assert.Equal(
            "restriction-loosened:sms.destination@" + Noon.ToString("O", CultureInfo.InvariantCulture),
            raised.IdempotencyKey);
    }
}
