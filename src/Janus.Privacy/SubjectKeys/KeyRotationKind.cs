using System.Text.Json.Serialization;

namespace Janus.Privacy.SubjectKeys;

/// <summary>
/// Which of the deployment's two keys a rotation replaces.
/// </summary>
/// <remarks>
/// Implements OPS-SEC-003 and CONV-ENUM-001. The two rotations share one shape and one
/// progress table, and each is resumed only by a run of its own kind.
/// </remarks>
internal enum KeyRotationKind
{
    /// <summary>The key-encryption key, whose rotation re-wraps what it wraps.</summary>
    [JsonStringEnumMemberName("key-encryption-key")]
    KeyEncryptionKey = 0,

    /// <summary>The fingerprint key, whose rotation re-computes every stored fingerprint.</summary>
    [JsonStringEnumMemberName("fingerprint-key")]
    FingerprintKey = 1,
}
