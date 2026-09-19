using Janus.Authentication.Passwords;
using Janus.Core.Configuration;
using Janus.Storage;
using Microsoft.Extensions.Logging;

namespace Janus.Hosting.Passwords;

/// <summary>
/// Where screening records that it ran on something other than the corpus the
/// deployment configured.
/// </summary>
/// <param name="log">The host's logger.</param>
/// <remarks>
/// Implements INT-PWD-002 and CONV-LOG-001. The two source names are the whole
/// entry: the prefix sent for screening is never logged (CONV-LOG-003).
/// </remarks>
internal sealed partial class ScreeningLog(ILogger<ScreeningLog> log) : IScreeningLog
{
    /// <inheritdoc/>
    public void Degraded(BlocklistSource configured, BlocklistSource used) =>
        Degraded(
            log,
            VocabularyConverter<BlocklistSource>.Write(configured),
            VocabularyConverter<BlocklistSource>.Write(used));

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Warning,
        Message = "The compromised-password corpus {Configured} could not answer; screening ran on {Used}.")]
    private static partial void Degraded(ILogger log, string configured, string used);
}
