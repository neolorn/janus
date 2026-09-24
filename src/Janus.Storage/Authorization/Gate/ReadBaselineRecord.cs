using Janus.Core;

namespace Janus.Storage.Authorization.Gate;

/// <summary>
/// The <c>read_baselines</c> row: one person's daily mean over the baseline window, as
/// last computed.
/// </summary>
/// <remarks>Implements OPS-ALERT-005.</remarks>
internal sealed class ReadBaselineRecord
{
    /// <summary>The <c>actor</c> column, which is this table's key.</summary>
    public SubjectId Actor { get; set; }

    /// <summary>The <c>daily_mean</c> column.</summary>
    public decimal DailyMean { get; set; }
}
