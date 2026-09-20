using System.Text.Json.Serialization;

namespace Janus.Core;

/// <summary>
/// Which capture path a consent runs through.
/// </summary>
/// <remarks>
/// Implements chapter 10 section 5.10, PRIV-BASIS-003, PRIV-CONS-004. Sensitive data
/// on a basis requiring it takes the written path; the distinction is what makes one
/// record distinguishable from the other afterwards.
/// </remarks>
public enum ConsentKind
{
    /// <summary>
    /// Affirmative action recorded against the notice version shown.
    /// </summary>
    [JsonStringEnumMemberName("ordinary")]
    Ordinary = 0,

    /// <summary>
    /// Captured electronically and retained, which sensitive data requires where its
    /// basis says so.
    /// </summary>
    [JsonStringEnumMemberName("written")]
    Written = 1,
}
