using System;
using Janus.Core;
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

    /// <summary>
    /// A return from the provider by a browser that started no sign-on, or whose
    /// first contact has since lapsed.
    /// </summary>
    /// <param name="log">The logger.</param>
    /// <param name="correlationId">What resolves the request.</param>
    [LoggerMessage(
        EventId = 8,
        Level = LogLevel.Warning,
        Message = "A sign-on return carried no attempt this browser had started ({CorrelationId}).")]
    public static partial void SignOnUnbound(ILogger log, string correlationId);

    /// <summary>
    /// A return whose state was absent or was not the one this browser was sent out
    /// with (BFF-SESS-006 AC3).
    /// </summary>
    /// <param name="log">The logger.</param>
    /// <param name="correlationId">What resolves the request.</param>
    [LoggerMessage(
        EventId = 9,
        Level = LogLevel.Warning,
        Message = "A sign-on return presented a state this browser was not sent out with ({CorrelationId}).")]
    public static partial void SignOnStateRejected(ILogger log, string correlationId);

    /// <summary>
    /// A return the provider refused, by the code it refused with.
    /// </summary>
    /// <param name="log">The logger.</param>
    /// <param name="correlationId">What resolves the request.</param>
    /// <param name="code">What the provider refused with.</param>
    [LoggerMessage(
        EventId = 10,
        Level = LogLevel.Warning,
        Message = "A sign-on was refused by the provider with {Code} ({CorrelationId}).")]
    public static partial void SignOnRefused(ILogger log, string correlationId, string code);

    /// <summary>
    /// An exchange the provider would not carry out, or whose identity token did not
    /// hold up (BFF-SESS-006 AC3).
    /// </summary>
    /// <param name="log">The logger.</param>
    /// <param name="correlationId">What resolves the request.</param>
    [LoggerMessage(
        EventId = 11,
        Level = LogLevel.Warning,
        Message = "A sign-on code was not exchanged for an identity token that held up ({CorrelationId}).")]
    public static partial void SignOnExchangeRejected(ILogger log, string correlationId);

    /// <summary>
    /// An authorization request the provider would not take when it was pushed
    /// (AUTH-OIDC-006 AC2).
    /// </summary>
    /// <param name="log">The logger.</param>
    /// <param name="correlationId">What resolves the request.</param>
    [LoggerMessage(
        EventId = 14,
        Level = LogLevel.Warning,
        Message = "A sign-on request was not taken by the provider when it was pushed ({CorrelationId}).")]
    public static partial void SignOnPushRejected(ILogger log, string correlationId);

    /// <summary>
    /// A sign-on by an application the provider's registry does not hold, which is a
    /// registration the deployment has not made.
    /// </summary>
    /// <param name="log">The logger.</param>
    /// <param name="correlationId">What resolves the request.</param>
    [LoggerMessage(
        EventId = 12,
        Level = LogLevel.Error,
        Message = "This application is not registered at the provider it signs on to ({CorrelationId}).")]
    public static partial void SignOnUnregistered(ILogger log, string correlationId);

    /// <summary>
    /// A cross-site post that navigated the whole page and carried no session, which
    /// was not carried and was answered with a read of the same address instead
    /// (BFF-CSRF-005 AC4).
    /// </summary>
    /// <param name="log">The logger.</param>
    /// <param name="correlationId">What resolves the request.</param>
    [LoggerMessage(
        EventId = 13,
        Level = LogLevel.Information,
        Message = "A cross-site POST navigation without a session was sent on as a GET ({CorrelationId}).")]
    public static partial void CrossSiteReturn(ILogger log, string correlationId);

    /// <summary>
    /// A refusal on a type that conceals, answered as the absence of the record
    /// (AUTHZ-CONCEAL-004).
    /// </summary>
    /// <param name="log">The logger.</param>
    /// <param name="correlationId">What resolves the request.</param>
    /// <param name="correlation">The audit record the refusal was written as.</param>
    [LoggerMessage(
        EventId = 15,
        Level = LogLevel.Information,
        Message = "The refusal recorded as {Correlation} was answered as an absent record ({CorrelationId}).")]
    public static partial void Concealed(ILogger log, string correlationId, Guid correlation);

    /// <summary>
    /// A refusal on a type that conceals, made after the endpoint had begun its answer,
    /// which could then only be broken off (BFF-ERR-003).
    /// </summary>
    /// <param name="log">The logger.</param>
    /// <param name="correlationId">What resolves the request.</param>
    /// <param name="correlation">The audit record the refusal was written as.</param>
    [LoggerMessage(
        EventId = 16,
        Level = LogLevel.Error,
        Message = "The refusal recorded as {Correlation} came after the answer had begun, so the connection was closed ({CorrelationId}).")]
    public static partial void ConcealedTooLate(ILogger log, string correlationId, Guid correlation);

    /// <summary>
    /// A round trip to a social provider that could not be bound to what the browser
    /// carries, or a return that found none bound (BFF-CSRF-005a).
    /// </summary>
    /// <param name="log">The logger.</param>
    /// <param name="correlationId">What resolves the request.</param>
    [LoggerMessage(
        EventId = 17,
        Level = LogLevel.Warning,
        Message = "A provider return carried no round trip this browser had started ({CorrelationId}).")]
    public static partial void ProviderUnbound(ILogger log, string correlationId);

    /// <summary>
    /// A provider return whose state was absent or was not the one this browser was
    /// sent out with (BFF-CSRF-005a).
    /// </summary>
    /// <param name="log">The logger.</param>
    /// <param name="correlationId">What resolves the request.</param>
    [LoggerMessage(
        EventId = 18,
        Level = LogLevel.Warning,
        Message = "A provider return presented a state this browser was not sent out with ({CorrelationId}).")]
    public static partial void ProviderStateRejected(ILogger log, string correlationId);

    /// <summary>
    /// A provider return that carried a refusal or no code. What the provider said is
    /// the provider's input and is not recorded.
    /// </summary>
    /// <param name="log">The logger.</param>
    /// <param name="correlationId">What resolves the request.</param>
    /// <param name="provider">Which provider.</param>
    [LoggerMessage(
        EventId = 19,
        Level = LogLevel.Information,
        Message = "A sign-in at {Provider} came back without a code ({CorrelationId}).")]
    public static partial void ProviderRefused(ILogger log, string correlationId, Factor provider);

    /// <summary>
    /// A code a provider would not exchange, or whose identity token did not hold up
    /// (IDN-LIFE-012, REG-IDENT-008).
    /// </summary>
    /// <param name="log">The logger.</param>
    /// <param name="correlationId">What resolves the request.</param>
    /// <param name="provider">Which provider.</param>
    [LoggerMessage(
        EventId = 20,
        Level = LogLevel.Warning,
        Message = "A code from {Provider} was not exchanged for an identity token that held up ({CorrelationId}).")]
    public static partial void ProviderExchangeRejected(ILogger log, string correlationId, Factor provider);

    /// <summary>
    /// A provider the deployment has not declared, or whose discovery document could
    /// not be read or names nowhere to sign in.
    /// </summary>
    /// <param name="log">The logger.</param>
    /// <param name="correlationId">What resolves the request.</param>
    /// <param name="provider">Which provider.</param>
    [LoggerMessage(
        EventId = 21,
        Level = LogLevel.Error,
        Message = "A sign-in at {Provider} could not be started: it is not declared or its discovery document could not be read ({CorrelationId}).")]
    public static partial void ProviderUnavailable(ILogger log, string correlationId, Factor provider);
}
