using System;
using System.Collections.Generic;
using System.Text.Json;
using Janus.Core;
using Microsoft.Extensions.Logging;

namespace Janus.Hosting.Bff;

/// <summary>
/// What the browser profile records when it refuses a request.
/// </summary>
/// <remarks>
/// Implements BFF-CSRF-001, BFF-CSRF-004, BFF-ERR-002, BFF-LOG-001, CONV-LOG-001,
/// CONV-LOG-002 and CONV-LOG-005. Which layer refused is recorded and never answered,
/// and no entry carries a cookie, a token or an address. An entry nothing writes is
/// declared no longer, and neither its identifier nor its name is given to another:
/// event 10 (<c>SignOnRefused</c>) is such a one.
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
    /// A request the framework could not bind to what the endpoint takes.
    /// </summary>
    /// <param name="log">The logger.</param>
    /// <param name="correlationId">What resolves the request.</param>
    /// <param name="member">The value or the member the reading stopped at, or nothing.</param>
    [LoggerMessage(
        EventId = 7,
        Level = LogLevel.Information,
        Message = "A request could not be bound at {Member} and was refused ({CorrelationId}).")]
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
    /// An exchange that could not be made or judged, the client's secret or the
    /// provider's published keys not being read, or whose identity token did not hold
    /// up or named no session (BFF-SESS-006 AC3). An exchange the provider refused is
    /// recorded once, where the browser is returned (BFF-LOG-001 AC2).
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
    public static partial void Concealed(ILogger log, string correlationId, AuditRecordId correlation);

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
    public static partial void ConcealedTooLate(ILogger log, string correlationId, AuditRecordId correlation);

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
    /// A round trip that could not reach or read its provider: the discovery document,
    /// which may also name nowhere to sign in, the published keys or the token
    /// endpoint (IDN-LIFE-012 AC6).
    /// </summary>
    /// <param name="log">The logger.</param>
    /// <param name="correlationId">What resolves the request.</param>
    /// <param name="provider">Which provider.</param>
    /// <param name="part">The part that could not be reached or read.</param>
    [LoggerMessage(
        EventId = 21,
        Level = LogLevel.Error,
        Message = "A round trip at {Provider} could not reach or read the provider's {Part} ({CorrelationId}).")]
    public static partial void ProviderUnavailable(ILogger log, string correlationId, Factor provider, string part);

    /// <summary>
    /// A request answered with a refusal, by the code the answer carried
    /// (BFF-LOG-001).
    /// </summary>
    /// <param name="log">The logger.</param>
    /// <param name="correlationId">What resolves the request.</param>
    /// <param name="code">The code the answer carried.</param>
    [LoggerMessage(
        EventId = 22,
        Level = LogLevel.Information,
        Message = "A request was refused with {Code} ({CorrelationId}).")]
    public static partial void Refused(ILogger log, string correlationId, ErrorCode code);

    /// <summary>
    /// A fault, by the code and the context the answer withheld, which are recorded
    /// here and nowhere else (BFF-ERR-002).
    /// </summary>
    /// <param name="log">The logger.</param>
    /// <param name="correlationId">What resolves the request.</param>
    /// <param name="code">The code behind the fault.</param>
    /// <param name="details">The fault's structured context.</param>
    [LoggerMessage(
        EventId = 23,
        Level = LogLevel.Error,
        Message = "A fault {Code} with {Details} was answered with neither ({CorrelationId}).")]
    public static partial void Faulted(
        ILogger log,
        string correlationId,
        ErrorCode code,
        IReadOnlyDictionary<string, JsonElement> details);

    /// <summary>
    /// A source that went over <c>abuse.source.ratelimit</c>, recorded once for the
    /// hold rather than for every request the hold refuses (BFF-ORDER-001 stage 4).
    /// </summary>
    /// <param name="log">The logger.</param>
    /// <param name="correlationId">What resolves the request.</param>
    /// <param name="lifts">When the source is admitted again.</param>
    [LoggerMessage(
        EventId = 24,
        Level = LogLevel.Warning,
        Message = "A source went over its request limit and is held until {Lifts} ({CorrelationId}).")]
    public static partial void SourceOverLimit(ILogger log, string correlationId, DateTimeOffset lifts);

    /// <summary>
    /// A round trip started at a provider the deployment does not declare
    /// (IDN-LIFE-012).
    /// </summary>
    /// <param name="log">The logger.</param>
    /// <param name="correlationId">What resolves the request.</param>
    /// <param name="provider">Which provider.</param>
    [LoggerMessage(
        EventId = 25,
        Level = LogLevel.Warning,
        Message = "A round trip was started at {Provider}, which the deployment does not declare ({CorrelationId}).")]
    public static partial void ProviderUndeclared(ILogger log, string correlationId, Factor provider);

    /// <summary>
    /// A sign-on refused and returned to where the browser was going, by the code it
    /// was returned with and, beside it, what the refusal carried inside: the error
    /// the provider, which is the library's own, refused with, or the library's own
    /// code (BFF-SESS-006, BFF-ERR-001 AC5).
    /// </summary>
    /// <param name="log">The logger.</param>
    /// <param name="correlationId">What resolves the request.</param>
    /// <param name="returned">The code the browser was returned with.</param>
    /// <param name="code">What the refusal carried inside.</param>
    [LoggerMessage(
        EventId = 26,
        Level = LogLevel.Information,
        Message = "A sign-on was returned with {Returned}, refused inside with {Code} ({CorrelationId}).")]
    public static partial void SignOnReturned(ILogger log, string correlationId, ErrorCode returned, string code);

    /// <summary>
    /// A sign-on fault that the provider, which is the library's own, answered,
    /// beside the fault's own entry: the status a push or an exchange was answered
    /// with, and the error the push, the exchange or the authorization response named,
    /// where it named one, and nothing else of the answer, never its description
    /// (BFF-SESS-006, BFF-ERR-001 AC5).
    /// </summary>
    /// <param name="log">The logger.</param>
    /// <param name="correlationId">What resolves the request.</param>
    /// <param name="status">The status read, or nothing for an authorization response.</param>
    /// <param name="error">The error read, or nothing where the answer named none.</param>
    [LoggerMessage(
        EventId = 27,
        Level = LogLevel.Error,
        Message = "A sign-on faulted on an answer of status {Status} naming the error {Error} ({CorrelationId}).")]
    public static partial void SignOnFaulted(ILogger log, string correlationId, int? status, string? error);
}
