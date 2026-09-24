using Microsoft.Extensions.Logging;

namespace Janus.Hosting.Background;

/// <summary>
/// What the worker records about a job that did not run as it should.
/// </summary>
/// <remarks>
/// Implements INF-BG-001, CONV-LOG-001 and CONV-LOG-003. A job is named by its
/// principal and a failure by its code, or by the type of what was thrown, so nothing
/// a run read reaches the log.
/// </remarks>
internal static partial class BackgroundLog
{
    /// <summary>
    /// A job's turn failed: its interval could not be read, or its run failed or threw.
    /// </summary>
    /// <param name="log">The logger.</param>
    /// <param name="job">Which job.</param>
    /// <param name="failure">The failure's code, or the type of what was thrown.</param>
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Error,
        Message = "The background job {Job} failed: {Failure}.")]
    public static partial void Failed(ILogger log, string job, string failure);

    /// <summary>
    /// Whether a job had lapsed could not be settled, or its alert could not be raised.
    /// </summary>
    /// <param name="log">The logger.</param>
    /// <param name="job">Which job.</param>
    /// <param name="failure">The failure's code, or the type of what was thrown.</param>
    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Error,
        Message = "The lapse of the background job {Job} was not raised: {Failure}.")]
    public static partial void LapseUnraised(ILogger log, string job, string failure);
}
