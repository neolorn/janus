using System;
using System.Collections.Generic;
using System.Net.Mime;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication;
using Janus.Authentication.Registration;
using Janus.Authentication.Sessions;
using Janus.Core;
using Janus.Core.Configuration;
using Janus.Hosting.Bff;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;

namespace Janus.Hosting.Registration;

/// <summary>
/// The registration endpoints of chapter 09 section 2.
/// </summary>
/// <remarks>
/// Implements CONV-DESIGN-006, API-CONV-001, BFF-CSRF-005b and REG-SESS-001 to
/// REG-SESS-007. Every path is relative to the group the host mounts, so no prefix is
/// assumed anywhere. Each endpoint reads the registration session from the
/// pre-authentication cookie and from nowhere else: a browser that does not carry it
/// did not start this registration, and it is refused whatever else it knows.
/// </remarks>
internal static class RegistrationEndpoints
{
    private static readonly IReadOnlyDictionary<string, bool> NoConsents =
        new Dictionary<string, bool>(StringComparer.Ordinal);

    private static readonly IResult Nothing = TypedResults.NoContent();

    private static readonly IResult Made = TypedResults.StatusCode(StatusCodes.Status201Created);

    /// <summary>
    /// Mounts them.
    /// </summary>
    /// <param name="endpoints">Where the host is mounting the library.</param>
    /// <returns>The builder, so the caller can go on.</returns>
    /// <exception cref="ArgumentNullException">The route builder is absent.</exception>
    public static IEndpointRouteBuilder MapRegistration(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        RouteGroupBuilder group = endpoints.MapGroup("/register");

        _ = group.MapPost("/", BeginAsync)
            .Declares(EndpointDeclaration
                .Answering(
                    ErrorCodes.ChallengeRequired, ErrorCodes.RegistrationSignedIn,
                    ErrorCodes.InvitationExpired, ErrorCodes.InvitationIdentifierMismatch,
                    ErrorCodes.Throttled))
            .Accepts<BeginRegistrationRequest>(MediaTypeNames.Application.Json)
            .Produces<RegistrationStateView>(StatusCodes.Status201Created);
        _ = group.MapGet("/", StateAsync)
            .Declares(EndpointDeclaration.Answering(ErrorCodes.SessionExpired))
            .Produces<RegistrationStateView>();
        _ = group.MapPut("/age", AgeAsync)
            .Declares(EndpointDeclaration
                .Answering(
                    ErrorCodes.SessionExpired, ErrorCodes.RegistrationIncomplete,
                    ErrorCodes.ProfileUnderage))
            .Accepts<AgeRequest>(MediaTypeNames.Application.Json)
            .Produces<RegistrationStateView>();
        _ = group.MapPut("/email", EmailAsync)
            .Declares(EndpointDeclaration
                .Answering(
                    ErrorCodes.SessionExpired, ErrorCodes.RegistrationIncomplete, ErrorCodes.Throttled,
                    ErrorCodes.RestrictionExceeded))
            .Accepts<IdentifierValueRequest>(MediaTypeNames.Application.Json)
            .Produces<RegistrationStateView>(StatusCodes.Status202Accepted);
        _ = group.MapPut("/phone", PhoneAsync)
            .Declares(EndpointDeclaration
                .Answering(
                    ErrorCodes.SessionExpired, ErrorCodes.RegistrationIncomplete, ErrorCodes.Throttled,
                    ErrorCodes.RestrictionExceeded, ErrorCodes.SmsBalanceFloor))
            .Accepts<IdentifierValueRequest>(MediaTypeNames.Application.Json)
            .Produces<RegistrationStateView>(StatusCodes.Status202Accepted);
        _ = group.MapPost("/phone/skip", SkipPhoneAsync)
            .Declares(EndpointDeclaration
                .Answering(
                    ErrorCodes.SessionExpired, ErrorCodes.RegistrationIncomplete))
            .Produces<RegistrationStateView>();
        _ = group.MapPost("/identifiers", AddAsync)
            .Declares(EndpointDeclaration
                .Answering(
                    ErrorCodes.SessionExpired, ErrorCodes.RegistrationIncomplete,
                    ErrorCodes.IdentifierMaximum, ErrorCodes.IdentifierDomainNotAllowed,
                    ErrorCodes.Throttled, ErrorCodes.RestrictionExceeded, ErrorCodes.SmsBalanceFloor))
            .Accepts<AddIdentifierRequest>(MediaTypeNames.Application.Json)
            .Produces<RegistrationStateView>(StatusCodes.Status202Accepted);
        _ = group.MapPut("/identifiers/{id}", ChangeAsync)
            .Declares(EndpointDeclaration
                .Answering(
                    ErrorCodes.SessionExpired, ErrorCodes.RegistrationIncomplete,
                    ErrorCodes.IdentifierLocked, ErrorCodes.IdentifierDomainNotAllowed,
                    ErrorCodes.Throttled, ErrorCodes.RestrictionExceeded, ErrorCodes.SmsBalanceFloor)
                .Binding<IdentifierId>("id"))
            .Accepts<IdentifierValueRequest>(MediaTypeNames.Application.Json)
            .Produces<RegistrationStateView>(StatusCodes.Status202Accepted);
        _ = group.MapDelete("/identifiers/{id}", DiscardAsync)
            .Declares(EndpointDeclaration
                .Answering(
                    ErrorCodes.SessionExpired, ErrorCodes.RegistrationIncomplete,
                    ErrorCodes.IdentifierLastOfKind)
                .Binding<IdentifierId>("id"))
            .Produces<RegistrationStateView>();
        _ = group.MapPost("/confirm", ConfirmAsync)
            .Declares(EndpointDeclaration
                .Answering(
                    ErrorCodes.SessionExpired, ErrorCodes.RegistrationIncomplete))
            .Produces<RegistrationStateView>();
        _ = group.MapPut("/security", SecurityAsync)
            .Declares(EndpointDeclaration
                .Answering(
                    ErrorCodes.RequestMalformed, ErrorCodes.SessionExpired,
                    ErrorCodes.RegistrationIncomplete, ErrorCodes.PasswordBlocklisted,
                    ErrorCodes.PasswordTooShort, ErrorCodes.PasswordTooLong))
            .Accepts<SecurityRequest>(MediaTypeNames.Application.Json)
            .Produces<RegistrationStateView>();
        _ = group.MapPost("/terms", TermsAsync)
            .Declares(EndpointDeclaration
                .Answering(
                    ErrorCodes.SessionExpired, ErrorCodes.RegistrationIncomplete,
                    ErrorCodes.NoticeUnpublished, ErrorCodes.AffirmationRequired,
                    ErrorCodes.PurposeNoConsent))
            .Accepts<TermsRequest>(MediaTypeNames.Application.Json)
            .Produces(StatusCodes.Status201Created);
        _ = group.MapPost("/verify/{id}", VerifyAsync)
            .Declares(EndpointDeclaration
                .Answering(
                    ErrorCodes.SessionExpired, ErrorCodes.CodeInvalid, ErrorCodes.CodeExpired,
                    ErrorCodes.Throttled, ErrorCodes.RestrictionExceeded, ErrorCodes.SmsBalanceFloor)
                .Binding<IdentifierId>("id"))
            .Accepts<VerifyRequest>(MediaTypeNames.Application.Json)
            .Produces<LinkLandingView>()
            .Produces(StatusCodes.Status204NoContent);
        _ = group.MapGet("/events", EventsAsync)
            .Declares(EndpointDeclaration.Answering(ErrorCodes.ResourceNotFound))
            .Produces<string>(StatusCodes.Status200OK, "text/event-stream");
        _ = group.MapPost("/abandon", AbandonAsync)
            .Declares(EndpointDeclaration.Answering())
            .Accepts<AbandonRequest>(MediaTypeNames.Application.Json)
            .Produces(StatusCodes.Status204NoContent);

        return endpoints;
    }

    // REG-SESS-002: a person already signed in is answered by the operation, which
    // creates no registration session for them, whatever else the request carries. The
    // account document is the account application's to fetch behind its own gate; a
    // registration route does not hand it out.
    private static async Task<IResult> BeginAsync(
        IRegistration registration,
        RequestSession browser,
        PreAuthenticationService contacts,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(registration);
        ArgumentNullException.ThrowIfNull(browser);
        ArgumentNullException.ThrowIfNull(contacts);
        ArgumentNullException.ThrowIfNull(context);

        BeginRegistrationRequest request = await JsonBody
            .ReadAsync<BeginRegistrationRequest>(context.Request, cancellationToken)
            .ConfigureAwait(false);

        if (browser.Context is null)
        {
            if (request.ClientId is not { Length: > 0 })
            {
                return Answers.Malformed("clientId");
            }

            if (browser.FirstContact is null)
            {
                return Gone();
            }
        }

        Error? failure = null;

        RegistrationSessionId session = (await registration
                .BeginAsync(
                    browser.Context,
                    request.ClientId ?? string.Empty,
                    RequestOrigin.Language(context.Request),
                    RequestOrigin.Address(context.Request),
                    RequestOrigin.Source(context.Request),
                    request.InvitationToken,
                    request.ChallengeToken,
                    cancellationToken)
                .ConfigureAwait(false))
            .Match(begun => begun, error => Withheld<RegistrationSessionId>(error, ref failure));

        if (failure is not null)
        {
            return Answers.Refused(failure);
        }

        RegistrationState state = (await registration
                .StateAsync(session, cancellationToken)
                .ConfigureAwait(false))
            .Match(read => read, error => Withheld<RegistrationState>(error, ref failure));

        if (failure is not null)
        {
            return Answers.Refused(failure);
        }

        // BFF-CSRF-005b: the cookie is what makes "the browser that started this" a
        // checkable fact rather than a hope.
        await contacts
            .CarryAsync(
                browser.FirstContact ?? throw new InvalidOperationException(
                    "A registration is begun only for a browser that carries its first contact."),
                session,
                state.ExpiresAt,
                cancellationToken)
            .ConfigureAwait(false);

        return Shown(state, StatusCodes.Status201Created);
    }

    private static async Task<IResult> StateAsync(
        IRegistration registration,
        RequestSession browser,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(registration);

        return Carried(browser) is not RegistrationSessionId session
            ? Gone()
            : Answers.Of(
                await registration.StateAsync(session, cancellationToken).ConfigureAwait(false),
                state => Shown(state, StatusCodes.Status200OK));
    }

    private static async Task<IResult> AgeAsync(
        IRegistration registration,
        RequestSession browser,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(registration);
        ArgumentNullException.ThrowIfNull(context);

        AgeRequest request = await JsonBody
            .ReadAsync<AgeRequest>(context.Request, cancellationToken)
            .ConfigureAwait(false);

        if (Carried(browser) is not RegistrationSessionId session)
        {
            return Gone();
        }

        return request.DateOfBirth is not DateOnly born
            ? Answers.Malformed("dateOfBirth")
            : Answers.Of(
                await registration
                    .RecordAgeAsync(session, born, cancellationToken)
                    .ConfigureAwait(false),
                state => Shown(state, StatusCodes.Status200OK));
    }

    private static Task<IResult> EmailAsync(
        IRegistration registration,
        RequestSession browser,
        HttpContext context,
        CancellationToken cancellationToken) =>
        StageAsync(IdentifierKind.Email, registration, browser, context, cancellationToken);

    private static Task<IResult> PhoneAsync(
        IRegistration registration,
        RequestSession browser,
        HttpContext context,
        CancellationToken cancellationToken) =>
        StageAsync(IdentifierKind.Phone, registration, browser, context, cancellationToken);

    private static async Task<IResult> SkipPhoneAsync(
        IRegistration registration,
        RequestSession browser,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(registration);

        return Carried(browser) is not RegistrationSessionId session
            ? Gone()
            : Answers.Of(
                await registration.SkipPhoneAsync(session, cancellationToken).ConfigureAwait(false),
                state => Shown(state, StatusCodes.Status200OK));
    }

    private static async Task<IResult> AddAsync(
        IRegistration registration,
        RequestSession browser,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(registration);

        AddIdentifierRequest request = await JsonBody
            .ReadAsync<AddIdentifierRequest>(context.Request, cancellationToken)
            .ConfigureAwait(false);

        if (Carried(browser) is not RegistrationSessionId session)
        {
            return Gone();
        }

        return request.Value is not { Length: > 0 } value
            ? Answers.Malformed("value")
            : Answers.Of(
                await registration
                    .AddAsync(session, request.Kind, value, RequestOrigin.Source(context.Request), cancellationToken)
                    .ConfigureAwait(false),
                state => Shown(state, StatusCodes.Status202Accepted));
    }

    private static async Task<IResult> ChangeAsync(
        IdentifierId id,
        IRegistration registration,
        RequestSession browser,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(registration);

        IdentifierValueRequest request = await JsonBody
            .ReadAsync<IdentifierValueRequest>(context.Request, cancellationToken)
            .ConfigureAwait(false);

        if (Carried(browser) is not RegistrationSessionId session)
        {
            return Gone();
        }

        return request.Value is not { Length: > 0 } value
            ? Answers.Malformed("value")
            : Answers.Of(
                await registration
                    .ChangeAsync(session, id, value, RequestOrigin.Source(context.Request), cancellationToken)
                    .ConfigureAwait(false),
                state => Shown(state, StatusCodes.Status202Accepted));
    }

    private static async Task<IResult> DiscardAsync(
        IdentifierId id,
        IRegistration registration,
        RequestSession browser,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(registration);

        return Carried(browser) is not RegistrationSessionId session
            ? Gone()
            : Answers.Of(
                await registration
                    .DiscardAsync(session, id, cancellationToken)
                    .ConfigureAwait(false),
                state => Shown(state, StatusCodes.Status200OK));
    }

    private static async Task<IResult> ConfirmAsync(
        IRegistration registration,
        RequestSession browser,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(registration);

        return Carried(browser) is not RegistrationSessionId session
            ? Gone()
            : Answers.Of(
                await registration.ConfirmAsync(session, cancellationToken).ConfigureAwait(false),
                state => Shown(state, StatusCodes.Status200OK));
    }

    // REG-SESS-006: the second step named in the request says which enrolment
    // ceremony the frontend runs next; what the library holds of it is what that
    // ceremony enrolled, which the state reports.
    private static async Task<IResult> SecurityAsync(
        IRegistration registration,
        RequestSession browser,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(registration);
        ArgumentNullException.ThrowIfNull(context);

        SecurityRequest request = await JsonBody
            .ReadAsync<SecurityRequest>(context.Request, cancellationToken)
            .ConfigureAwait(false);

        if (Carried(browser) is not RegistrationSessionId session)
        {
            return Gone();
        }

        Result<RegistrationState> outcome = request.Password is { Length: > 0 } password
            ? await registration
                .SetPasswordAsync(session, password, cancellationToken)
                .ConfigureAwait(false)
            : await registration.StateAsync(session, cancellationToken).ConfigureAwait(false);

        return Answers.Of(outcome, state => Shown(state, StatusCodes.Status200OK));
    }

    private static async Task<IResult> TermsAsync(
        RegistrationService registration,
        RequestSession browser,
        PreAuthenticationService contacts,
        BrowserSessionCookies cookies,
        IConfigurationStore configuration,
        TimeProvider time,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(registration);
        ArgumentNullException.ThrowIfNull(contacts);
        ArgumentNullException.ThrowIfNull(cookies);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(time);
        ArgumentNullException.ThrowIfNull(context);

        TermsRequest request = await JsonBody
            .ReadAsync<TermsRequest>(context.Request, cancellationToken)
            .ConfigureAwait(false);

        if (Carried(browser) is not RegistrationSessionId session)
        {
            return Gone();
        }

        if (request.TermsVersion is not { Length: > 0 } terms)
        {
            return Answers.Malformed("termsVersion");
        }

        if (request.NoticeVersion is not { Length: > 0 } notice)
        {
            return Answers.Malformed("noticeVersion");
        }

        SessionOrigin origin = RequestOrigin.Of(context.Request);
        Error? failure = null;

        RegistrationOutcome completed = (await registration
                .CompleteAsync(
                    session,
                    terms,
                    notice,
                    request.Consents ?? NoConsents,
                    origin.Device,
                    origin.Address,
                    cancellationToken)
                .ConfigureAwait(false))
            .Match(outcome => outcome, error => Withheld<RegistrationOutcome>(error, ref failure));

        if (failure is not null)
        {
            return Answers.Refused(failure);
        }

        TimeSpan remembered = (await configuration
                .ReadAsync(Settings.DeviceVerificationLifetime, cancellationToken)
                .ConfigureAwait(false))
            .Match(read => read, error => Withheld<TimeSpan>(error, ref failure));

        if (failure is not null)
        {
            return Answers.Refused(failure);
        }

        // BFF-CSRF-005a AC3: the pre-authentication session becomes the real one
        // rather than standing beside it, so it ends here and not at its expiry.
        if (context.Request.Cookies[BrowserCookies.PreAuthentication] is { Length: > 0 } first)
        {
            await contacts
                .RotateAsync(OpaqueToken.Of(first), cancellationToken)
                .ConfigureAwait(false);
        }

        cookies.Write(context.Response, completed.Session);
        cookies.ClearFirstContact(context.Response);
        cookies.Remembered(
            context.Response,
            completed.Browser,
            time.GetUtcNow() + remembered);

        return Made;
    }

    private static async Task<IResult> VerifyAsync(
        IdentifierId id,
        IRegistration registration,
        RequestSession browser,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(registration);

        VerifyRequest request = await JsonBody
            .ReadAsync<VerifyRequest>(context.Request, cancellationToken)
            .ConfigureAwait(false);

        // API-LAND-001: a link merely opened, or opened somewhere else, changes
        // nothing and is answered with the code to type instead.
        if (request.LinkToken is { Length: > 0 } token)
        {
            return Answers.Of(
                await registration
                    .LandAsync(
                        Carried(browser),
                        token,
                        request.Press,
                        RequestOrigin.Source(context.Request),
                        cancellationToken)
                    .ConfigureAwait(false),
                Landed);
        }

        if (Carried(browser) is not RegistrationSessionId session)
        {
            return Gone();
        }

        return request.Code is not { Length: > 0 } code
            ? Answers.Malformed("code")
            : Answers.Of(
                await registration
                    .VerifyAsync(session, id, code, RequestOrigin.Source(context.Request), cancellationToken)
                    .ConfigureAwait(false),
                _ => Nothing);
    }

    // REG-SESS-001: a token that resolves to no live session is answered identically,
    // so the answer says nothing about whether one existed.
    private static async Task<IResult> AbandonAsync(
        IRegistration registration,
        RequestSession browser,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(registration);
        ArgumentNullException.ThrowIfNull(context);

        AbandonRequest request = await JsonBody
            .ReadAsync<AbandonRequest>(context.Request, cancellationToken)
            .ConfigureAwait(false);

        return Answers.Of(
            await registration
                .AbandonAsync(Carried(browser), request.LinkToken, cancellationToken)
                .ConfigureAwait(false),
            Nothing);
    }

    private static Task EventsAsync(
        IRegistration registration,
        RequestSession browser,
        IRegistrationSignals signals,
        IConfigurationStore configuration,
        HttpContext context,
        CancellationToken cancellationToken) =>
        Carried(browser) is not RegistrationSessionId session
            ? RegistrationStream.NothingAsync(context)
            : RegistrationStream.RunAsync(
                registration,
                session,
                signals,
                configuration,
                context,
                cancellationToken);

    private static async Task<IResult> StageAsync(
        IdentifierKind kind,
        IRegistration registration,
        RequestSession browser,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(registration);
        ArgumentNullException.ThrowIfNull(context);

        IdentifierValueRequest request = await JsonBody
            .ReadAsync<IdentifierValueRequest>(context.Request, cancellationToken)
            .ConfigureAwait(false);

        if (Carried(browser) is not RegistrationSessionId session)
        {
            return Gone();
        }

        // API-CONV-005: accepted whether or not the identifier belongs to an account.
        return request.Value is not { Length: > 0 } value
            ? Answers.Malformed("value")
            : Answers.Of(
                await registration
                    .StageAsync(session, kind, value, RequestOrigin.Source(context.Request), cancellationToken)
                    .ConfigureAwait(false),
                state => Shown(state, StatusCodes.Status202Accepted));
    }

    private static RegistrationSessionId? Carried(RequestSession browser)
    {
        ArgumentNullException.ThrowIfNull(browser);

        return browser.FirstContact?.Registration;
    }

    private static IResult Gone() => Answers.Refused(ErrorCodes.SessionExpired);

    private static IResult Landed(LinkLanding landing)
    {
        ArgumentNullException.ThrowIfNull(landing);

        if (landing.Verified)
        {
            return Nothing;
        }

        return TypedResults.Json(
            LinkLandingView.Of(landing),
            RegistrationJson.Default.LinkLandingView,
            contentType: null,
            StatusCodes.Status200OK);
    }

    private static JsonHttpResult<RegistrationStateView> Shown(RegistrationState state, int status) =>
        TypedResults.Json(
            RegistrationStateView.Of(state),
            RegistrationJson.Default.RegistrationStateView,
            contentType: null,
            status);

    private static TValue Withheld<TValue>(Error error, ref Error? failure)
    {
        failure = error;

        return default!;
    }
}
