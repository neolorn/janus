using Microsoft.Extensions.Logging;

namespace Janus.Hosting.Bff;

/// <summary>
/// What the browser profile records when it refuses a request.
/// </summary>
/// <remarks>
/// Implements BFF-CSRF-001, BFF-CSRF-004, CONV-LOG-001, CONV-LOG-002 and
/// CONV-LOG-005. Which layer refused is recorded and never answered, and no entry
/// carries a cookie, a token or an address.
/// </remarks>
internal static partial class BrowserProfileLog
{
    /// <summary>
    /// A cross-site request that would have changed state.
    /// </summary>
    /// <param name="log">The logger.</param>
    /// <param name="correlationId">What resolves the request.</param>
    /// <param name="method">The method it arrived with.</param>
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Warning,
        Message = "A cross-site {Method} was refused by the resource isolation policy ({CorrelationId}).")]
    public static partial void CrossSite(ILogger log, string correlationId, string method);

    /// <summary>
    /// A state-changing request that carried no custom header.
    /// </summary>
    /// <param name="log">The logger.</param>
    /// <param name="correlationId">What resolves the request.</param>
    /// <param name="method">The method it arrived with.</param>
    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Warning,
        Message = "A {Method} without the custom request header was refused ({CorrelationId}).")]
    public static partial void HeaderAbsent(ILogger log, string correlationId, string method);

    /// <summary>
    /// A state-changing request whose origin was not the target's.
    /// </summary>
    /// <param name="log">The logger.</param>
    /// <param name="correlationId">What resolves the request.</param>
    /// <param name="expected">The origin the request arrived at.</param>
    [LoggerMessage(
        EventId = 3,
        Level = LogLevel.Warning,
        Message = "A request claiming another origin than {Expected} was refused ({CorrelationId}).")]
    public static partial void OriginMismatch(ILogger log, string correlationId, string expected);

    /// <summary>
    /// A state-changing request whose synchronizer token was absent or did not belong
    /// to the session it arrived on.
    /// </summary>
    /// <param name="log">The logger.</param>
    /// <param name="correlationId">What resolves the request.</param>
    /// <param name="method">The method it arrived with.</param>
    [LoggerMessage(
        EventId = 4,
        Level = LogLevel.Warning,
        Message = "A {Method} without a session-bound synchronizer token was refused ({CorrelationId}).")]
    public static partial void TokenRejected(ILogger log, string correlationId, string method);

    /// <summary>
    /// A browser arrived holding nothing and could not be given a pre-authentication
    /// session, so the request goes on without one and every state change it tries is
    /// refused.
    /// </summary>
    /// <param name="log">The logger.</param>
    /// <param name="correlationId">What resolves the request.</param>
    /// <param name="code">The code the refusal carried.</param>
    [LoggerMessage(
        EventId = 5,
        Level = LogLevel.Warning,
        Message = "A browser was given no pre-authentication session: {Code} ({CorrelationId}).")]
    public static partial void FirstContactRefused(ILogger log, string correlationId, string code);

    /// <summary>
    /// A request whose body the reader could not turn into what the endpoint takes.
    /// </summary>
    /// <param name="log">The logger.</param>
    /// <param name="correlationId">What resolves the request.</param>
    /// <param name="member">The member the reader stopped at, or nothing.</param>
    [LoggerMessage(
        EventId = 7,
        Level = LogLevel.Information,
        Message = "A request body could not be read at {Member} and was refused ({CorrelationId}).")]
    public static partial void BodyUnreadable(ILogger log, string correlationId, string? member);

    /// <summary>
    /// A request to a machine endpoint that carried a session cookie.
    /// </summary>
    /// <param name="log">The logger.</param>
    /// <param name="correlationId">What resolves the request.</param>
    /// <param name="method">What it asked for.</param>
    [LoggerMessage(
        EventId = 6,
        Level = LogLevel.Warning,
        Message = "A {Method} on the machine profile carried a session cookie and was refused ({CorrelationId}).")]
    public static partial void CookieOnMachineProfile(ILogger log, string correlationId, string method);
}
