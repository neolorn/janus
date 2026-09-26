using System;

namespace Janus.Storage.Authentication.Background;

/// <summary>
/// The <c>background_jobs</c> row: one job, and when it was first seen, last attempted,
/// last succeeded and last raised as lapsed.
/// </summary>
/// <remarks>
/// Implements INF-BG-001. The row is what makes a job that stopped running noticed:
/// the worker compares the last success with the interval rather than waiting to be
/// told of a failure.
/// </remarks>
internal sealed class BackgroundJobRecord
{
    /// <summary>The <c>name</c> column, which is this table key.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>The <c>recorded_at</c> column.</summary>
    public DateTimeOffset RecordedAt { get; set; }

    /// <summary>The <c>attempted_at</c> column.</summary>
    public DateTimeOffset AttemptedAt { get; set; }

    /// <summary>The <c>succeeded_at</c> column.</summary>
    public DateTimeOffset? SucceededAt { get; set; }

    /// <summary>The <c>lapse_raised_at</c> column.</summary>
    public DateTimeOffset? LapseRaisedAt { get; set; }
}
