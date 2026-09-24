using System;
using Janus.Core;

namespace Janus.Storage.Authorization.Gate;

/// <summary>
/// The <c>read_volume</c> row: how many records one person was given on one calendar
/// day.
/// </summary>
/// <remarks>
/// Implements OPS-ALERT-005. A row older than the baseline window is forgotten by the
/// job that recomputes the means.
/// </remarks>
internal sealed class ReadVolumeRecord
{
    /// <summary>The <c>actor</c> column.</summary>
    public SubjectId Actor { get; set; }

    /// <summary>The <c>day</c> column.</summary>
    public DateOnly Day { get; set; }

    /// <summary>The <c>records</c> column.</summary>
    public long Records { get; set; }
}
