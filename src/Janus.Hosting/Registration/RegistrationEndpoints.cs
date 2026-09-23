using System;
using System.Collections.Generic;
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

        _ = group.MapPost("/", BeginAsync);
        _ = group.MapGet("/", StateAsync);
        _ = group.MapPut("/age", AgeAsync);
        _ = group.MapPut("/email", EmailAsync);
        _ = group.MapPut("/phone", PhoneAsync);
        _ = group.MapPost("/phone/skip", SkipPhoneAsync);
        _ = group.MapPost("/identifiers", AddAsync);
        _ = group.MapPut("/identifiers/{id:guid}", ChangeAsync);
        _ = group.MapDelete("/identifiers/{id:guid}", DiscardAsync);
        _ = group.MapPost("/confirm", ConfirmAsync);
        _ = group.MapPut("/security", SecurityAsync);
        _ = group.MapPost("/terms", TermsAsync);
        _ = group.MapPost("/verify/{id:guid}", VerifyAsync);
        _ = group.MapGet("/events", EventsAsync);
        _ = group.MapPost("/abandon", AbandonAsync);

        return endpoints;
    }

    // REG-SESS-002: a person already signed in is refused and sent to their account,
    // and no registration session is created for them. An invitation link they press
    // attaches to that account, whose membership step reads it (REG-INV-002).
    private static async Task<IResult> BeginAsync(
        BeginRegistrationRequest request,
        IRegistration registration,
        IInvitations invitations,
        RequestSession browser,
        PreAuthenticationService contacts,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(registration);
        ArgumentNullException.ThrowIfNull(invitations);
        ArgumentNullException.ThrowIfNull(browser);
        ArgumentNullException.ThrowIfNull(contacts);
        ArgumentNullException.ThrowIfNull(context);

        if (browser.Context is AccessContext signedIn)
        {
            if (request.InvitationToken is string token
                && (await invitations.OpenAsync(signedIn, token, cancellationToken).ConfigureAwait(false))
                    .Match(() => (Error?)null, error => error) is Error unopened)
            {
                return Answers.Refused(unopened);
            }

            // The account document is the account application's to fetch behind its own
            // gate; a registration route does not hand it out (REG-SESS-002).
            return Answers.Refused(ErrorCodes.RegistrationSignedIn);
        }

        if (request.ClientId is not { Length: > 0 } client)
        {
            return Answers.Malformed("clientId");
        }

        if (browser.FirstContact is not PreAuthentication contact)
        {
            return Gone();
        }

        Error? failure = null;

        RegistrationSessionId session = (await registration
                .BeginAsync(
                    client,
                    RequestOrigin.Language(context.Request),
                    RequestOrigin.Source(context.Request),
                    request.InvitationToken,
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
            .CarryAsync(contact, session, state.ExpiresAt, cancellationToken)
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
        AgeRequest request,
        IRegistration registration,
        RequestSession browser,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(registration);

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
        IdentifierValueRequest request,
        IRegistration registration,
        RequestSession browser,
        CancellationToken cancellationToken) =>
        StageAsync(request, IdentifierKind.Email, registration, browser, cancellationToken);

    private static Task<IResult> PhoneAsync(
        IdentifierValueRequest request,
        IRegistration registration,
        RequestSession browser,
        CancellationToken cancellationToken) =>
        StageAsync(request, IdentifierKind.Phone, registration, browser, cancellationToken);

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
        AddIdentifierRequest request,
        IRegistration registration,
        RequestSession browser,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(registration);

        if (Carried(browser) is not RegistrationSessionId session)
        {
            return Gone();
        }

        return request.Value is not { Length: > 0 } value
            ? Answers.Malformed("value")
            : Answers.Of(
                await registration
                    .AddAsync(session, request.Kind, value, cancellationToken)
                    .ConfigureAwait(false),
                state => Shown(state, StatusCodes.Status202Accepted));
    }

    private static async Task<IResult> ChangeAsync(
        Guid id,
        IdentifierValueRequest request,
        IRegistration registration,
        RequestSession browser,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(registration);

        if (Carried(browser) is not RegistrationSessionId session)
        {
            return Gone();
        }

        return request.Value is not { Length: > 0 } value
            ? Answers.Malformed("value")
            : Answers.Of(
                await registration
                    .ChangeAsync(session, new IdentifierId(id), value, cancellationToken)
                    .ConfigureAwait(false),
                state => Shown(state, StatusCodes.Status202Accepted));
    }

    private static async Task<IResult> DiscardAsync(
        Guid id,
        IRegistration registration,
        RequestSession browser,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(registration);

        return Carried(browser) is not RegistrationSessionId session
            ? Gone()
            : Answers.Of(
                await registration
                    .DiscardAsync(session, new IdentifierId(id), cancellationToken)
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
        SecurityRequest request,
        IRegistration registration,
        RequestSession browser,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(registration);

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
        TermsRequest request,
        RegistrationService registration,
        RequestSession browser,
        PreAuthenticationService contacts,
        BrowserSessionCookies cookies,
        IConfigurationStore configuration,
        TimeProvider time,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(registration);
        ArgumentNullException.ThrowIfNull(contacts);
        ArgumentNullException.ThrowIfNull(cookies);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(time);
        ArgumentNullException.ThrowIfNull(context);

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
        Guid id,
        VerifyRequest request,
        IRegistration registration,
        RequestSession browser,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(registration);

        // API-LAND-001: a link merely opened, or opened somewhere else, changes
        // nothing and is answered with the code to type instead.
        if (request.LinkToken is { Length: > 0 } token)
        {
            return Answers.Of(
                await registration
                    .LandAsync(Carried(browser), token, request.Press, cancellationToken)
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
                    .VerifyAsync(session, new IdentifierId(id), code, cancellationToken)
                    .ConfigureAwait(false),
                _ => Nothing);
    }

    // REG-SESS-001: a token that resolves to no live session is answered identically,
    // so the answer says nothing about whether one existed.
    private static async Task<IResult> AbandonAsync(
        AbandonRequest request,
        IRegistration registration,
        RequestSession browser,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(registration);

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
        IdentifierValueRequest request,
        IdentifierKind kind,
        IRegistration registration,
        RequestSession browser,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(registration);

        if (Carried(browser) is not RegistrationSessionId session)
        {
            return Gone();
        }

        // API-CONV-005: accepted whether or not the identifier belongs to an account.
        return request.Value is not { Length: > 0 } value
            ? Answers.Malformed("value")
            : Answers.Of(
                await registration
                    .StageAsync(session, kind, value, cancellationToken)
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
