using System;
using Janus.Core;

namespace Janus.Storage.Privacy.Erasures;

/// <summary>
/// The <c>erasures</c> row.
/// </summary>
/// <remarks>
/// Implements IDN-LIFE-003b and CONV-DESIGN-003. No column of this table is on the
/// account: an erasure is a background operation and not a phase of a person's life.
/// </remarks>
internal sealed class ErasureRecord
{
    /// <summary>
    /// The <c>subject</c> column, which is this table's key.
    /// </summary>
    public SubjectId Subject { get; set; }

    /// <summary>
    /// The <c>requested_at</c> column.
    /// </summary>
    public DateTimeOffset RequestedAt { get; set; }

    /// <summary>
    /// The <c>reason</c> column.
    /// </summary>
    public ErasureReason Reason { get; set; }

    /// <summary>
    /// The <c>status</c> column: how far the host-side work has got.
    /// </summary>
    public ErasureStatus Status { get; set; }

    /// <summary>
    /// The <c>attempts</c> column: delivery attempts across subscribers.
    /// </summary>
    public int Attempts { get; set; }
}
