using System;
using Janus.Authentication.Events;

namespace Janus.Storage.Authentication.Events;

/// <summary>
/// The <c>events</c> row: one emitted event and how far its consumers have got.
/// </summary>
/// <remarks>Implements LIB-API-001 and CONV-DESIGN-002.</remarks>
internal sealed class PendingEventRecord
{
    /// <summary>The <c>id</c> column, which is this table key.</summary>
    public PendingEventId Id { get; set; }

    /// <summary>The <c>kind</c> column, the event's name in chapter 10 section 5b.</summary>
    public string Kind { get; set; } = string.Empty;

    /// <summary>The <c>raised_at</c> column.</summary>
    public DateTimeOffset RaisedAt { get; set; }

    /// <summary>The <c>payload</c> column, the event as a JSON object.</summary>
    public string Payload { get; set; } = string.Empty;

    /// <summary>The <c>attempts</c> column.</summary>
    public int Attempts { get; set; }

    /// <summary>The <c>next_attempt_at</c> column.</summary>
    public DateTimeOffset NextAttemptAt { get; set; }

    /// <summary>The <c>taken_by</c> column, a JSON array of consumer names.</summary>
    public string TakenBy { get; set; } = "[]";

    /// <summary>The <c>published_at</c> column, which marks the row.</summary>
    public DateTimeOffset? PublishedAt { get; set; }

    /// <summary>The <c>failed_at</c> column.</summary>
    public DateTimeOffset? FailedAt { get; set; }
}
