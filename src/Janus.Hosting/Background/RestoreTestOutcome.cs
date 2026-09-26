using System.Text.Json.Serialization;

namespace Janus.Hosting.Background;

/// <summary>
/// What one run of the automated restore test found, in the spelling its record and its
/// alert carry.
/// </summary>
/// <remarks>
/// Implements DR-007 AC2 and AC3, as entry 334 of the decisions pending review settles
/// them. Each failure names the step the test did not get past, so the operator knows
/// which of the backup, the key-encryption key and the fingerprint key to look at.
/// </remarks>
internal enum RestoreTestOutcome
{
    /// <summary>
    /// The canary's field decrypted and its verified email found its account, within
    /// the objective.
    /// </summary>
    [JsonStringEnumMemberName("passed")]
    Passed = 0,

    /// <summary>
    /// Nothing was restored: the deployment registered nothing to restore with, or the
    /// restore failed.
    /// </summary>
    [JsonStringEnumMemberName("unrestored")]
    Unrestored = 1,

    /// <summary>
    /// The canary's field did not decrypt under the live key-encryption key, or the
    /// restored database does not hold it.
    /// </summary>
    [JsonStringEnumMemberName("undecrypted")]
    Undecrypted = 2,

    /// <summary>
    /// The canary's field decrypted, but its verified email did not find its account
    /// under the live fingerprint key.
    /// </summary>
    [JsonStringEnumMemberName("unresolved")]
    Unresolved = 3,

    /// <summary>
    /// The objective passed before the test had proved the restore.
    /// </summary>
    [JsonStringEnumMemberName("overrun")]
    Overrun = 4,
}
