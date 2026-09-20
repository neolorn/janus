using System;
using System.Collections.Generic;
using Janus.Core;
using Janus.Privacy.Outbox;

namespace Janus.Storage.Privacy.Outbox;

/// <summary>
/// The <c>outbox</c> row.
/// </summary>
/// <remarks>
/// Implements IDN-LIFE-003a and CONV-DESIGN-003. The row is written in the
/// transaction that made the fact true, so a fact without its delivery, or a delivery
/// without its fact, cannot exist.
/// </remarks>
internal sealed class DeliveryRecord
{
    /// <summary>
    /// The <c>id</c> column, which is this table's key.
    /// </summary>
    public DeliveryId Id { get; set; }

    /// <summary>
    /// The <c>subject</c> column.
    /// </summary>
    public SubjectId Subject { get; set; }

    /// <summary>
    /// The <c>kind</c> column: which fact the delivery carries.
    /// </summary>
    public SubjectEventKind Kind { get; set; }

    /// <summary>
    /// The <c>raised_at</c> column.
    /// </summary>
    public DateTimeOffset RaisedAt { get; set; }

    /// <summary>
    /// The <c>restricted</c> column, read where the fact is a restriction change.
    /// </summary>
    public bool Restricted { get; set; }

    /// <summary>
    /// The <c>reason</c> column, read where the fact is an erasure.
    /// </summary>
    public ErasureReason Reason { get; set; }

    /// <summary>
    /// The <c>status</c> column: how far the subscribers have got.
    /// </summary>
    public ErasureStatus Status { get; set; }

    /// <summary>
    /// The <c>attempts</c> column.
    /// </summary>
    public int Attempts { get; set; }

    /// <summary>
    /// The <c>next_attempt_at</c> column.
    /// </summary>
    public DateTimeOffset NextAttemptAt { get; set; }

    /// <summary>
    /// The confirmations this delivery has collected.
    /// </summary>
    public ICollection<DeliveryConfirmationRecord> Confirmations { get; } = [];
}
