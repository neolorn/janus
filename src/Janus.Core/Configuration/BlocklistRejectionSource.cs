using System.Text.Json.Serialization;

namespace Janus.Core.Configuration;

/// <summary>
/// A source a password is screened against before it is accepted. Adding one is a
/// tightening; the leaked list cannot be removed.
/// </summary>
/// <remarks>Implements chapter 10 section 4.2, AUTH-PASS-004.</remarks>
public enum BlocklistRejectionSource
{
    /// <summary>
    /// The compromised-password corpus.
    /// </summary>
    [JsonStringEnumMemberName("leaked")]
    Leaked = 0,

    /// <summary>
    /// A word list.
    /// </summary>
    [JsonStringEnumMemberName("dictionary")]
    Dictionary = 1,

    /// <summary>
    /// The person's own identifiers, profile fields and the service name.
    /// </summary>
    [JsonStringEnumMemberName("context")]
    Context = 2,
}
