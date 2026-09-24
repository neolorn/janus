using System;

namespace Janus.Storage.Authentication.Sending;

/// <summary>
/// The <c>registration_sources</c> row: one registration session started from one
/// source.
/// </summary>
/// <remarks>
/// Implements AUTH-ABUSE-008. The source is held by its hash: the row counts, it
/// does not identify.
/// </remarks>
internal sealed class RegistrationSourceRecord
{
    /// <summary>The <c>id</c> column, which is this table key.</summary>
    public Guid Id { get; set; }

    /// <summary>The <c>source</c> column.</summary>
    public byte[] Source { get; set; } = [];

    /// <summary>
    /// The <c>fingerprint_version</c> column: the version of the fingerprint key the
    /// source is hashed under.
    /// </summary>
    public int FingerprintVersion { get; set; }

    /// <summary>The <c>at</c> column.</summary>
    public DateTimeOffset At { get; set; }
}
