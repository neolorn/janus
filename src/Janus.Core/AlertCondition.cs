using System.Text.Json.Serialization;

namespace Janus.Core;

/// <summary>
/// One condition of OPS-ALERT-001, in the order that item's table gives them. The
/// raised alert carries the identifier and deduplication keys on it.
/// </summary>
/// <remarks>
/// Implements chapter 10 section 5.23, OPS-ALERT-001, OPS-ALERT-002, D-153.
/// </remarks>
public enum AlertCondition
{
    /// <summary>Sustained authentication failures against one account.</summary>
    [JsonStringEnumMemberName("auth-failures-sustained")]
    AuthFailuresSustained = 0,

    /// <summary>Recovery attempts clustering on one account.</summary>
    [JsonStringEnumMemberName("recovery-clustering")]
    RecoveryClustering = 1,

    /// <summary>One approver handling unusual recovery volume.</summary>
    [JsonStringEnumMemberName("approver-volume")]
    ApproverVolume = 2,

    /// <summary>Read-volume or export anomaly for one actor.</summary>
    [JsonStringEnumMemberName("read-volume-anomaly")]
    ReadVolumeAnomaly = 3,

    /// <summary>The emergency credential was used.</summary>
    [JsonStringEnumMemberName("breakglass-used")]
    BreakGlassUsed = 4,

    /// <summary>A protected setting changed.</summary>
    [JsonStringEnumMemberName("protected-setting-changed")]
    ProtectedSettingChanged = 5,

    /// <summary>An alert destination changed; delivered to the previous destinations.</summary>
    [JsonStringEnumMemberName("alert-destination-changed")]
    AlertDestinationChanged = 6,

    /// <summary>A step-up policy was weakened.</summary>
    [JsonStringEnumMemberName("stepup-policy-weakened")]
    StepUpPolicyWeakened = 7,

    /// <summary>Two sessions of one account were used implausibly far apart.</summary>
    [JsonStringEnumMemberName("concurrent-sessions-implausible")]
    ConcurrentSessionsImplausible = 8,

    /// <summary>A spike in permission denials for one actor.</summary>
    [JsonStringEnumMemberName("denial-spike")]
    DenialSpike = 9,

    /// <summary>The gateway balance is draining or has breached its floor.</summary>
    [JsonStringEnumMemberName("sms-balance")]
    SmsBalance = 10,

    /// <summary>A background job failed.</summary>
    [JsonStringEnumMemberName("background-job-failed")]
    BackgroundJobFailed = 11,

    /// <summary>An erasure or takedown delivery exhausted its retries.</summary>
    [JsonStringEnumMemberName("erasure-delivery-exhausted")]
    ErasureDeliveryExhausted = 12,

    /// <summary>A certificate renewal failed.</summary>
    [JsonStringEnumMemberName("certificate-renewal-failed")]
    CertificateRenewalFailed = 13,

    /// <summary>The host clock drifted beyond tolerance.</summary>
    [JsonStringEnumMemberName("clock-drift")]
    ClockDrift = 14,

    /// <summary>
    /// A degradation: blocklist fallback, a failed provider push, an undelivered
    /// notification or reconciliation drift.
    /// </summary>
    [JsonStringEnumMemberName("degradation")]
    Degradation = 15,

    /// <summary>Repeated callback verification failure from one source.</summary>
    [JsonStringEnumMemberName("callback-verification-failed")]
    CallbackVerificationFailed = 16,

    /// <summary>An unusual rate of duplicate-identifier notices: an enumeration probe.</summary>
    [JsonStringEnumMemberName("nonexistent-notice-rate")]
    NonexistentNoticeRate = 17,

    /// <summary>A privacy-request decision deadline is approaching.</summary>
    [JsonStringEnumMemberName("privacy-deadline-approaching")]
    PrivacyDeadlineApproaching = 18,

    /// <summary>A privacy-request decision deadline was reached undecided.</summary>
    [JsonStringEnumMemberName("privacy-deadline-reached")]
    PrivacyDeadlineReached = 19,

    /// <summary>A licence or permit expiry is approaching.</summary>
    [JsonStringEnumMemberName("expiry-approaching")]
    ExpiryApproaching = 20,

    /// <summary>The public-holiday list is running out.</summary>
    [JsonStringEnumMemberName("holiday-list-exhausted")]
    HolidayListExhausted = 21,

    /// <summary>No emergency credential exists.</summary>
    [JsonStringEnumMemberName("no-emergency-credential")]
    NoEmergencyCredential = 22,

    /// <summary>A restore test failed or exceeded the recovery-time objective.</summary>
    [JsonStringEnumMemberName("restore-test-failed")]
    RestoreTestFailed = 23,

    /// <summary>A restriction was loosened.</summary>
    [JsonStringEnumMemberName("restriction-loosened")]
    RestrictionLoosened = 24,

    /// <summary>A restriction grant was issued.</summary>
    [JsonStringEnumMemberName("restriction-granted")]
    RestrictionGranted = 25,

    /// <summary>A locked domain's record no longer verifies.</summary>
    [JsonStringEnumMemberName("domain-reverification-failed")]
    DomainReverificationFailed = 26,

    /// <summary>A domain was removed from a lock.</summary>
    [JsonStringEnumMemberName("domain-removed")]
    DomainRemoved = 27,

    /// <summary>The governing language of legal documents changed.</summary>
    [JsonStringEnumMemberName("governing-language-changed")]
    GoverningLanguageChanged = 28,

    /// <summary>A document has no text in the governing language.</summary>
    [JsonStringEnumMemberName("governing-text-missing")]
    GoverningTextMissing = 29,

    /// <summary>The sending domain is not registered with the private relay.</summary>
    [JsonStringEnumMemberName("relay-domain-unregistered")]
    RelayDomainUnregistered = 30,
}
