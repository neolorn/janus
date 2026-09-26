using System;
using Janus.Core;
using Janus.Privacy.SubjectKeys;

namespace Janus.Storage.Privacy.SubjectKeys;

/// <summary>
/// The <c>key_rotations</c> row: one rotation of one key, and how far it has gone.
/// </summary>
/// <remarks>
/// Implements OPS-SEC-003 and OPS-MIG-003a. The maintenance credential reads and writes
/// the table and the application's reaches none of it.
/// </remarks>
internal sealed class KeyRotationRecord
{
    /// <summary>The <c>kind</c> column, the first part of this table's key.</summary>
    public KeyRotationKind Kind { get; set; }

    /// <summary>The <c>version</c> column, the second part of this table's key.</summary>
    public int Version { get; set; }

    /// <summary>The <c>last_subject</c> column.</summary>
    public SubjectId? LastSubject { get; set; }

    /// <summary>The <c>processed</c> column.</summary>
    public int Processed { get; set; }

    /// <summary>The <c>started_at</c> column.</summary>
    public DateTimeOffset StartedAt { get; set; }

    /// <summary>The <c>completed_at</c> column.</summary>
    public DateTimeOffset? CompletedAt { get; set; }

    /// <summary>The <c>retired_at</c> column.</summary>
    public DateTimeOffset? RetiredAt { get; set; }
}
