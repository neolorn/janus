using System;

namespace Janus.Storage.Identity.Identifiers;

/// <summary>
/// The <c>username_holds</c> row.
/// </summary>
/// <remarks>
/// Implements REG-IDENT-009 and CONV-DESIGN-003. Erasure destroys the key the username
/// was stored under and neutralises its fingerprint, so what holds the name is this
/// row and its fingerprint alone; a username is public by nature, so nothing here is a
/// personal field under a key that no longer exists.
/// </remarks>
internal sealed class UsernameHoldRecord
{
    /// <summary>
    /// The <c>fingerprint</c> column, which is this table's key.
    /// </summary>
    public byte[] Fingerprint { get; set; } = [];

    /// <summary>
    /// The <c>fingerprint_version</c> column: the version of the fingerprint key the
    /// fingerprint was computed under.
    /// </summary>
    public int FingerprintVersion { get; set; }

    /// <summary>
    /// The <c>held_from</c> column: the erasure the hold runs from.
    /// </summary>
    public DateTimeOffset HeldFrom { get; set; }

    /// <summary>
    /// The <c>releases_at</c> column: when the name can be claimed again.
    /// </summary>
    public DateTimeOffset ReleasesAt { get; set; }
}
