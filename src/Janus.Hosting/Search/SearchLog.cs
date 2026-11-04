using System;
using Janus.Authentication.Search;
using Microsoft.Extensions.Logging;

namespace Janus.Hosting.Search;

/// <summary>
/// What the staff search writes to the log: a search's result count and duration, the
/// memory checks, and the index's own faults, and nothing of a query or an indexed value.
/// </summary>
/// <param name="log">The host's logger.</param>
/// <remarks>Implements IDN-SRCH-002, IDN-SRCH-003, CONV-LOG-001 and CONV-LOG-003 (D-256).</remarks>
internal sealed partial class SearchLog(ILogger<SearchLog> log) : ISearchLog
{
    /// <inheritdoc/>
    public void Searched(int results, TimeSpan duration) => Answered(log, results, duration.TotalMilliseconds);

    /// <summary>
    /// A memory check failed and the index is built anyway, under <c>warn</c>.
    /// </summary>
    /// <param name="log">The logger.</param>
    /// <param name="check">Which check, by name.</param>
    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Warning,
        Message = "The staff search's memory check {Check} failed; the index is built under warn.")]
    public static partial void CheckFailed(ILogger log, string check);

    /// <summary>
    /// A step of the index's keeping failed, and is tried again.
    /// </summary>
    /// <param name="log">The logger.</param>
    /// <param name="failure">The failure's code, or the fault log entry of what was thrown.</param>
    [LoggerMessage(
        EventId = 3,
        Level = LogLevel.Error,
        Message = "The staff search's index could not be kept: {Failure}.")]
    public static partial void Failed(ILogger log, string failure);

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Debug,
        Message = "A staff search answered {Results} results in {Milliseconds} ms.")]
    private static partial void Answered(ILogger log, int results, double milliseconds);
}
