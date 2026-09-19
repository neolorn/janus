using System;
using Janus.Core;

namespace Janus.Storage.Authentication.Passwords;

/// <summary>
/// The <c>passwords</c> row.
/// </summary>
/// <remarks>
/// Implements CONV-DESIGN-003. The row holds the encoded hash and the floor the
/// password met when it was set, which cannot be recomputed from the hash
/// (AUTH-PASS-001a).
/// </remarks>
internal sealed class PasswordRecord
{
    /// <summary>
    /// The <c>subject</c> column, which is this table's key: an account holds one
    /// password.
    /// </summary>
    public SubjectId Subject { get; set; }

    /// <summary>
    /// The <c>hash</c> column, holding the parameters the hash was computed at.
    /// </summary>
    public string Hash { get; set; } = string.Empty;

    /// <summary>
    /// The <c>meets_single_factor_floor</c> column.
    /// </summary>
    public bool MeetsSingleFactorFloor { get; set; }

    /// <summary>
    /// The <c>set_at</c> column.
    /// </summary>
    public DateTimeOffset SetAt { get; set; }
}
