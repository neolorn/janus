using System.Text.Json.Serialization;

namespace Janus.Core;

/// <summary>
/// The pool-wide work a deployment-scoped system principal exists for. The set is
/// enumerated because a principal that may cross organizations may do these things and
/// nothing else.
/// </summary>
/// <remarks>Implements IDN-PRIN-001.</remarks>
public enum SystemOperation
{
    /// <summary>
    /// Comparing the library's records against an external system and reporting the
    /// difference.
    /// </summary>
    [JsonStringEnumMemberName("reconciliation")]
    Reconciliation = 0,

    /// <summary>
    /// Purging what has passed its declared retention.
    /// </summary>
    [JsonStringEnumMemberName("retention-purge")]
    RetentionPurge = 1,

    /// <summary>
    /// Generating the records of processing.
    /// </summary>
    [JsonStringEnumMemberName("records-of-processing")]
    RecordsOfProcessing = 2,

    /// <summary>
    /// Sweeping what has expired: sessions, codes, grace windows and the like.
    /// </summary>
    [JsonStringEnumMemberName("expiry-sweep")]
    ExpirySweep = 3,

    /// <summary>
    /// Carrying what has been committed to where it goes: the outbox, the mailboxes
    /// owed to the mail server and the raised alerts.
    /// </summary>
    [JsonStringEnumMemberName("delivery")]
    Delivery = 4,

    /// <summary>
    /// Reading the state of something the deployment depends on and raising the alert
    /// its reading calls for.
    /// </summary>
    [JsonStringEnumMemberName("monitoring")]
    Monitoring = 5,

    /// <summary>
    /// Standing a fresh deployment up: its named values, its administrative roles and
    /// organization, and the accounts it starts with.
    /// </summary>
    [JsonStringEnumMemberName("bootstrap")]
    Bootstrap = 6,
}
