using Microsoft.Extensions.Logging;

namespace Janus.Hosting.Callbacks;

/// <summary>
/// What the machine profile records of a host's callback it did not carry.
/// </summary>
/// <remarks>
/// Implements BFF-MACH-003, INT-GEN-003, CONV-LOG-001 and CONV-LOG-003. An entry names
/// the callback, the check that refused it and the correlation identifier; never a
/// reference, a signature, a secret or anything of the body.
/// </remarks>
internal static partial class CallbackLog
{
    /// <summary>
    /// A callback refused by one of the checks.
    /// </summary>
    /// <param name="log">The logger.</param>
    /// <param name="callback">The callback's name.</param>
    /// <param name="check">The check that refused it.</param>
    /// <param name="correlationId">What resolves the request.</param>
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Warning,
        Message = "A callback to {Callback} was refused by its {Check} check ({CorrelationId}).")]
    public static partial void Refused(ILogger log, string callback, CallbackCheck check, string correlationId);

    /// <summary>
    /// A delivery of an event already carried, acknowledged without being carried again.
    /// </summary>
    /// <param name="log">The logger.</param>
    /// <param name="callback">The callback's name.</param>
    /// <param name="correlationId">What resolves the request.</param>
    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Information,
        Message = "A repeated delivery to {Callback} was acknowledged and not carried ({CorrelationId}).")]
    public static partial void Repeated(ILogger log, string callback, string correlationId);
}
