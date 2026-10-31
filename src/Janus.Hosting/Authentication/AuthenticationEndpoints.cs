using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mime;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication;
using Janus.Authentication.Factors;
using Janus.Authentication.Sessions;
using Janus.Authentication.SignIn;
using Janus.Core;
using Janus.Core.Configuration;
using Janus.Hosting.Bff;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Janus.Hosting.Authentication;

/// <summary>
/// The authentication endpoints of chapter 09 section 3.
/// </summary>
/// <remarks>
/// Implements CONV-DESIGN-006, API-CONV-001, LIB-API-005, AUTH-FACT-001,
/// AUTH-FACT-015, AUTH-FACT-016, AUTH-FACT-017 and AUTH-STEP-001. What a sign-in
/// hands the browser is written here and nowhere else: the library answers with what
/// was reached, and the cookies that carry the session, the remembered browser and
/// the trusted device belong to the boundary (BFF-OWN-001).
/// </remarks>
internal static class AuthenticationEndpoints
{
    private static readonly IResult Nothing = TypedResults.NoContent();

    private static readonly IResult Accepted = TypedResults.StatusCode(StatusCodes.Status202Accepted);

    /// <summary>
    /// Mounts them.
    /// </summary>
    /// <param name="endpoints">Where the host is mounting the library.</param>
    /// <returns>The builder, so the caller can go on.</returns>
    /// <exception cref="ArgumentNullException">The route builder is absent.</exception>
    public static IEndpointRouteBuilder MapAuthentication(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        RouteGroupBuilder group = endpoints.MapGroup("/auth");

        _ = group.MapPost("/begin", BeginAsync)
            .Declares(EndpointDeclaration.Answering())
            .Accepts<SignInRequest>(MediaTypeNames.Application.Json)
            .Produces<SignInChallengeView>();
        _ = group.MapPost("/factor", PresentAsync)
            .Declares(EndpointDeclaration
                .Answering(
                    ErrorCodes.RequestMalformed, ErrorCodes.PolicyGraceExpired,
                    ErrorCodes.FactorRejected, ErrorCodes.FactorNotPermitted,
                    ErrorCodes.IdentifierDomainNotAllowed, ErrorCodes.CodeInvalid,
                    ErrorCodes.CodeExpired, ErrorCodes.CodeReplayed, ErrorCodes.CredentialSuspended,
                    ErrorCodes.WebAuthnAlgorithmNotAllowed, ErrorCodes.WebAuthnCounterMismatch,
                    ErrorCodes.WebAuthnRelyingPartyChanged, ErrorCodes.WebAuthnUserVerificationRequired,
                    ErrorCodes.Throttled, ErrorCodes.RestrictionExceeded))
            .Accepts<PresentFactorRequest>(MediaTypeNames.Application.Json)
            .Produces<SignInProgressView>()
            .Produces<SignInLandingView>()
            .Produces(StatusCodes.Status202Accepted);
        _ = group.MapPost("/device/verify", VerifyDeviceAsync)
            .Declares(EndpointDeclaration
                .Answering(
                    ErrorCodes.CodeInvalid, ErrorCodes.CodeExpired, ErrorCodes.RestrictionExceeded))
            .Accepts<VerifyDeviceRequest>(MediaTypeNames.Application.Json)
            .Produces<SignInProgressView>();
        _ = group.MapPost("/device/resend", ResendDeviceAsync)
            .Declares(EndpointDeclaration
                .Answering(ErrorCodes.CodeExpired, ErrorCodes.RestrictionExceeded, ErrorCodes.Throttled))
            .Accepts<ResendDeviceRequest>(MediaTypeNames.Application.Json)
            .Produces(StatusCodes.Status202Accepted);
        _ = SessionRequired.On(group.MapPost("/step-up", StepUpAsync))
            .Declares(EndpointDeclaration
                .Answering(
                    ErrorCodes.StepUpRequired, ErrorCodes.FactorNotPermitted, ErrorCodes.FactorRejected,
                    ErrorCodes.IdentifierDomainNotAllowed, ErrorCodes.CodeInvalid, ErrorCodes.CodeExpired,
                    ErrorCodes.CodeReplayed, ErrorCodes.CredentialSuspended,
                    ErrorCodes.WebAuthnAlgorithmNotAllowed,
                    ErrorCodes.WebAuthnCounterMismatch, ErrorCodes.WebAuthnRelyingPartyChanged,
                    ErrorCodes.WebAuthnUserVerificationRequired, ErrorCodes.Throttled,
                    ErrorCodes.RestrictionExceeded))
            .Accepts<PresentFactorRequest>(MediaTypeNames.Application.Json)
            .Produces<SignInProgressView>()
            .Produces(StatusCodes.Status202Accepted);
        _ = group.MapPost("/link", LinkAsync)
            .Declares(EndpointDeclaration.Answering(ErrorCodes.Throttled, ErrorCodes.RestrictionExceeded))
            .Accepts<SignInRequest>(MediaTypeNames.Application.Json)
            .Produces(StatusCodes.Status202Accepted);
        _ = group.MapPost("/link/abandon", AbandonLinkAsync)
            .Declares(EndpointDeclaration.Answering())
            .Accepts<AbandonLinkRequest>(MediaTypeNames.Application.Json)
            .Produces(StatusCodes.Status204NoContent);
        _ = group.MapPost("/email-otp", CodeAsync)
            .Declares(EndpointDeclaration.Answering(ErrorCodes.Throttled, ErrorCodes.RestrictionExceeded))
            .Accepts<SignInRequest>(MediaTypeNames.Application.Json)
            .Produces(StatusCodes.Status202Accepted);
        _ = SessionRequired.On(group.MapPost("/logout", LogoutAsync))
            .Declares(EndpointDeclaration.Answering())
            .Produces(StatusCodes.Status204NoContent);
        _ = SessionRequired.On(group.MapGet("/session", SessionAsync))
            .Declares(EndpointDeclaration.Answering())
            .Produces<SessionDetailView>();

        // Chapter 09 section 3 lists the browsers the account knows under the account
        // and the sessions elsewhere, so the two lists are never read as one
        // (AUTH-SESS-013).
        RouteGroupBuilder devices = endpoints.MapGroup("/account/devices");

        _ = SessionRequired.On(devices.MapGet("/", ListDevicesAsync))
            .Declares(EndpointDeclaration.Answering())
            .Produces<IReadOnlyList<DeviceView>>();
        _ = SessionRequired.On(devices.MapDelete("/{id}", ForgetDeviceAsync))
            .Declares(EndpointDeclaration.Answering(ErrorCodes.ResourceNotFound).Binding<DeviceId>("id"))
            .Produces(StatusCodes.Status204NoContent);

        return endpoints;
    }

    // AUTH-ABUSE-001 AC5: the browser's own tokens travel with the sign-in it opens, so
    // a browser the account knows is not held by an attack on the account; the
    // sign-in is bound to the browser's pre-authentication session (D-226).
    private static async Task<IResult> BeginAsync(
        IAuthentication authentication,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(authentication);
        ArgumentNullException.ThrowIfNull(context);

        SignInRequest request = await JsonBody
            .ReadAsync<SignInRequest>(context.Request, cancellationToken)
            .ConfigureAwait(false);

        return request.Identifier is not { Length: > 0 } identifier
            ? Answers.Malformed("identifier")
            : Answers.Of(
                await authentication
                    .BeginAsync(
                        identifier,
                        RequestOrigin.Source(context.Request),
                        Carried(context.Request, BrowserCookies.Browser),
                        Carried(context.Request, BrowserCookies.Device),
                        Carried(context.Request, BrowserCookies.PreAuthentication),
                        cancellationToken)
                    .ConfigureAwait(false),
                challenge => TypedResults.Json(
                    SignInChallengeView.Of(challenge),
                    AuthenticationJson.Default.SignInChallengeView,
                    contentType: null,
                    StatusCodes.Status200OK));
    }

    // REG-SESS-003, API-LAND-001: a link completes in the browser its sign-in is bound
    // to and on a press; the same call from anywhere else changes nothing and carries
    // back the code to type where the sign-in began. A link's token names its sign-in,
    // so a challengeId beside it is not read (D-224 item 17).
    private static async Task<IResult> PresentAsync(
        AuthenticationService authentication,
        BrowserSessionCookies cookies,
        PreAuthenticationService contacts,
        IConfigurationStore configuration,
        TimeProvider time,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(authentication);
        ArgumentNullException.ThrowIfNull(context);

        PresentFactorRequest request = await JsonBody
            .ReadAsync<PresentFactorRequest>(context.Request, cancellationToken)
            .ConfigureAwait(false);

        SessionOrigin origin = RequestOrigin.Of(context.Request);

        if (request.LinkToken is { Length: > 0 } token)
        {
            // A link token is answered only under a link factor, which is what a press
            // that opens nothing is recorded under (CONV-LOG-005).
            if (!FactorCatalogue.Sent.Any(sent => sent.Key.CarriesLink && sent.Value == request.Factor))
            {
                return Answers.Malformed("factor");
            }

            return await LandedAsync(
                    await authentication
                        .LandAsync(
                            Carried(context.Request, BrowserCookies.PreAuthentication),
                            token,
                            request.Factor,
                            request.Press,
                            origin,
                            Carried(context.Request, BrowserCookies.Browser),
                            cancellationToken)
                        .ConfigureAwait(false),
                    cookies,
                    contacts,
                    configuration,
                    time,
                    context,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        if (request.ChallengeId is not { Length: > 0 } challenge)
        {
            return Answers.Malformed("challengeId");
        }

        if (request.Presented() is not FactorPresentation presented)
        {
            return Answers.Malformed("value");
        }

        if (Asking(request))
        {
            return Answers.Of(
                await authentication
                    .AskAsync(
                        challenge,
                        request.Factor,
                        stepping: null,
                        session: null,
                        origin.Source,
                        RequestOrigin.Language(context.Request),
                        cancellationToken)
                    .ConfigureAwait(false),
                Asked);
        }

        return await ReachedAsync(
                await authentication
                    .PresentAsync(
                        challenge,
                        presented,
                        origin,
                        Carried(context.Request, BrowserCookies.Browser),
                        Carried(context.Request, BrowserCookies.Device),
                        cancellationToken)
                    .ConfigureAwait(false),
                cookies,
                contacts,
                configuration,
                time,
                context,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static async Task<IResult> VerifyDeviceAsync(
        AuthenticationService authentication,
        BrowserSessionCookies cookies,
        PreAuthenticationService contacts,
        IConfigurationStore configuration,
        TimeProvider time,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(authentication);
        ArgumentNullException.ThrowIfNull(context);

        VerifyDeviceRequest request = await JsonBody
            .ReadAsync<VerifyDeviceRequest>(context.Request, cancellationToken)
            .ConfigureAwait(false);

        if (request.ChallengeId is not { Length: > 0 } challenge)
        {
            return Answers.Malformed("challengeId");
        }

        if (request.Code is not { Length: > 0 } code)
        {
            return Answers.Malformed("code");
        }

        return await ReachedAsync(
                await authentication
                    .VerifyDeviceAsync(
                        challenge,
                        code,
                        RequestOrigin.Of(context.Request),
                        cancellationToken)
                    .ConfigureAwait(false),
                cookies,
                contacts,
                configuration,
                time,
                context,
                cancellationToken)
            .ConfigureAwait(false);
    }

    // AUTH-FACT-016 AC9 (D-224 item 18): a new code is sent only to the browser the
    // held sign-in is bound to (D-226).
    private static async Task<IResult> ResendDeviceAsync(
        IAuthentication authentication,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(authentication);
        ArgumentNullException.ThrowIfNull(context);

        ResendDeviceRequest request = await JsonBody
            .ReadAsync<ResendDeviceRequest>(context.Request, cancellationToken)
            .ConfigureAwait(false);

        return request.ChallengeId is not { Length: > 0 } challenge
            ? Answers.Malformed("challengeId")
            : Answers.Of(
                await authentication
                    .ResendDeviceCodeAsync(
                        challenge,
                        Carried(context.Request, BrowserCookies.PreAuthentication),
                        RequestOrigin.Source(context.Request),
                        cancellationToken)
                    .ConfigureAwait(false),
                Accepted);
    }

    private static async Task<IResult> StepUpAsync(
        AuthenticationService authentication,
        RequestSession browser,
        BrowserSessionCookies cookies,
        PreAuthenticationService contacts,
        IConfigurationStore configuration,
        TimeProvider time,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(authentication);
        ArgumentNullException.ThrowIfNull(browser);
        ArgumentNullException.ThrowIfNull(context);

        PresentFactorRequest request = await JsonBody
            .ReadAsync<PresentFactorRequest>(context.Request, cancellationToken)
            .ConfigureAwait(false);

        AccessContext holder = Asking(browser);

        if (request.ChallengeId is not { Length: > 0 } challenge)
        {
            return Answers.Malformed("challengeId");
        }

        if (request.Presented() is not FactorPresentation presented)
        {
            return Answers.Malformed("value");
        }

        if (Asking(request))
        {
            return Answers.Of(
                await authentication
                    .AskAsync(
                        challenge,
                        request.Factor,
                        holder.Effective,
                        browser.Required.Id,
                        RequestOrigin.Source(context.Request),
                        RequestOrigin.Language(context.Request),
                        cancellationToken)
                    .ConfigureAwait(false),
                Asked);
        }

        return await ReachedAsync(
                await authentication
                    .RaiseAsync(
                        holder,
                        browser.Required.Id,
                        challenge,
                        presented,
                        RequestOrigin.Source(context.Request),
                        cancellationToken)
                    .ConfigureAwait(false),
                cookies,
                contacts,
                configuration,
                time,
                context,
                cancellationToken)
            .ConfigureAwait(false);
    }

    // AUTH-ABUSE-003: accepted whether or not the identifier belongs to an account
    // and whether or not the policy enables the entry the link would be.
    private static async Task<IResult> LinkAsync(
        IAuthentication authentication,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(authentication);
        ArgumentNullException.ThrowIfNull(context);

        SignInRequest request = await JsonBody
            .ReadAsync<SignInRequest>(context.Request, cancellationToken)
            .ConfigureAwait(false);

        return request.Identifier is not { Length: > 0 } identifier
            ? Answers.Malformed("identifier")
            : Answers.Of(
                await authentication
                    .SendLinkAsync(
                        identifier,
                        RequestOrigin.Language(context.Request),
                        RequestOrigin.Source(context.Request),
                        Carried(context.Request, BrowserCookies.PreAuthentication),
                        cancellationToken)
                    .ConfigureAwait(false),
                Accepted);
    }

    private static async Task<IResult> CodeAsync(
        IAuthentication authentication,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(authentication);
        ArgumentNullException.ThrowIfNull(context);

        SignInRequest request = await JsonBody
            .ReadAsync<SignInRequest>(context.Request, cancellationToken)
            .ConfigureAwait(false);

        return request.Identifier is not { Length: > 0 } identifier
            ? Answers.Malformed("identifier")
            : Answers.Of(
                await authentication
                    .SendCodeAsync(
                        identifier,
                        RequestOrigin.Language(context.Request),
                        RequestOrigin.Source(context.Request),
                        cancellationToken)
                    .ConfigureAwait(false),
                Accepted);
    }

    // REG-SESS-003: a token that resolves to no pending link is answered identically,
    // so the answer says nothing about whether one stood.
    private static async Task<IResult> AbandonLinkAsync(
        IAuthentication authentication,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(authentication);
        ArgumentNullException.ThrowIfNull(context);

        AbandonLinkRequest request = await JsonBody
            .ReadAsync<AbandonLinkRequest>(context.Request, cancellationToken)
            .ConfigureAwait(false);

        return request.LinkToken is not { Length: > 0 } token
            ? Answers.Malformed("linkToken")
            : Answers.Of(
                await authentication.AbandonLinkAsync(token, cancellationToken).ConfigureAwait(false),
                Nothing);
    }

    // AUTH-SESS-008: every session of the account ends, not the calling application's
    // alone, and the browser is left carrying neither cookie.
    private static async Task<IResult> LogoutAsync(
        ISessions sessions,
        RequestSession browser,
        BrowserSessionCookies cookies,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sessions);
        ArgumentNullException.ThrowIfNull(browser);
        ArgumentNullException.ThrowIfNull(cookies);
        ArgumentNullException.ThrowIfNull(context);

        AccessContext holder = Asking(browser);

        Error? failure = null;

        _ = (await sessions.EndEverywhereAsync(holder, cancellationToken).ConfigureAwait(false))
            .Match(() => true, error => Withheld<bool>(error, ref failure));

        if (failure is not null)
        {
            return Answers.Refused(failure);
        }

        cookies.Clear(context.Response);

        return Nothing;
    }

    private static async Task<IResult> SessionAsync(
        ISessions sessions,
        RequestSession browser,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sessions);
        ArgumentNullException.ThrowIfNull(browser);

        AccessContext holder = Asking(browser);

        return Answers.Of(
            await sessions
                .ReadAsync(holder, browser.Required.Id, cancellationToken)
                .ConfigureAwait(false),
            session => TypedResults.Json(
                SessionDetailView.Of(session),
                AuthenticationJson.Default.SessionDetailView,
                contentType: null,
                StatusCodes.Status200OK));
    }

    private static async Task<IResult> ListDevicesAsync(
        IAuthentication authentication,
        RequestSession browser,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(authentication);
        ArgumentNullException.ThrowIfNull(browser);

        AccessContext holder = Asking(browser);

        return Answers.Of(
            await authentication
                .ListDevicesAsync(holder, cancellationToken)
                .ConfigureAwait(false),
            known => TypedResults.Json<IReadOnlyList<DeviceView>>(
                [.. known.Select(DeviceView.Of)],
                AuthenticationJson.Default.IReadOnlyListDeviceView,
                contentType: null,
                StatusCodes.Status200OK));
    }

    private static async Task<IResult> ForgetDeviceAsync(
        DeviceId id,
        IAuthentication authentication,
        RequestSession browser,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(authentication);
        ArgumentNullException.ThrowIfNull(browser);

        AccessContext holder = Asking(browser);

        return Answers.Of(
            await authentication
                .ForgetDeviceAsync(holder, id, cancellationToken)
                .ConfigureAwait(false),
            Nothing);
    }

    // AUTH-FACT-002 AC6: a second step the library texts is asked for by naming it with
    // nothing to present, and the ask is answered 202 whatever it finds (D-166).
    private static bool Asking(PresentFactorRequest request) =>
        !request.Presents() && AuthenticationService.Asks(request.Factor);

    private static async Task<IResult> LandedAsync(
        Result<LandedSignIn> outcome,
        BrowserSessionCookies cookies,
        PreAuthenticationService contacts,
        IConfigurationStore configuration,
        TimeProvider time,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        Error? failure = null;
        LandedSignIn landed = outcome
            .Match(value => value, error => Withheld<LandedSignIn>(error, ref failure));

        if (failure is not null)
        {
            return Answers.Refused(failure);
        }

        if (landed.Outcome is not SignInOutcome signedIn)
        {
            return TypedResults.Json(
                SignInLandingView.Of(landed),
                AuthenticationJson.Default.SignInLandingView,
                contentType: null,
                StatusCodes.Status200OK);
        }

        return await CarriedAsync(signedIn, cookies, contacts, configuration, time, context, cancellationToken)
            .ConfigureAwait(false);
    }

    // AUTH-FACT-002: an ask is accepted, save at a sign-in or a step-up whose number's
    // signal withheld the code, which is answered with what the challenge then offers.
    private static IResult Asked(SignInProgress? offered) =>
        offered is null
            ? Accepted
            : TypedResults.Json(
                SignInProgressView.Of(offered),
                AuthenticationJson.Default.SignInProgressView,
                contentType: null,
                StatusCodes.Status200OK);

    private static async Task<IResult> ReachedAsync(
        Result<SignInOutcome> outcome,
        BrowserSessionCookies cookies,
        PreAuthenticationService contacts,
        IConfigurationStore configuration,
        TimeProvider time,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        Error? failure = null;
        SignInOutcome reached = outcome
            .Match(value => value, error => Withheld<SignInOutcome>(error, ref failure));

        if (failure is not null)
        {
            return Answers.Refused(failure);
        }

        return await CarriedAsync(reached, cookies, contacts, configuration, time, context, cancellationToken)
            .ConfigureAwait(false);
    }

    // AUTH-SESS-006, AUTH-FACT-015, AUTH-FACT-016: the session pair is written again
    // by every step that issued one, which is what rotation looks like at the
    // boundary, and each lasting cookie is written only where the step produced one.
    // BFF-CSRF-005a AC3 (D-206): the first contact the browser carried is ended where
    // a session is issued, rather than left beside it until its expiry.
    private static async Task<IResult> CarriedAsync(
        SignInOutcome outcome,
        BrowserSessionCookies cookies,
        PreAuthenticationService contacts,
        IConfigurationStore configuration,
        TimeProvider time,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(cookies);
        ArgumentNullException.ThrowIfNull(contacts);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(time);

        if (outcome.Session is IssuedSession issued)
        {
            if (Carried(context.Request, BrowserCookies.PreAuthentication) is string first)
            {
                await contacts.RotateAsync(OpaqueToken.Of(first), cancellationToken).ConfigureAwait(false);
            }

            cookies.Write(context.Response, issued);
            cookies.ClearFirstContact(context.Response);
        }

        DateTimeOffset now = time.GetUtcNow();

        if (outcome.Remembered is OpaqueToken remembered)
        {
            TimeSpan lasts = await ForAsync(
                    configuration,
                    Settings.DeviceVerificationLifetime,
                    cancellationToken)
                .ConfigureAwait(false);

            cookies.Remembered(context.Response, remembered, now + lasts);
        }

        if (outcome.Trusted is OpaqueToken trusted)
        {
            TimeSpan lasts = await ForAsync(
                    configuration,
                    Settings.FactorTrustedDeviceLifetime,
                    cancellationToken)
                .ConfigureAwait(false);

            cookies.Trusted(context.Response, trusted, now + lasts);
        }

        return TypedResults.Json(
            SignInProgressView.Of(outcome.Progress),
            AuthenticationJson.Default.SignInProgressView,
            contentType: null,
            StatusCodes.Status200OK);
    }

    // BFF-STEP-001: the endpoints that read this are mounted as ones that need a
    // session, so the stage that requires one has already answered a request that
    // arrived without it.
    private static AccessContext Asking(RequestSession browser)
    {
        ArgumentNullException.ThrowIfNull(browser);

        return browser.Asking;
    }

    // How long the browser is to carry what it was just handed. A deployment that has
    // set nothing gets the shipped default, which is what the store answers with.
    private static async ValueTask<TimeSpan> ForAsync(
        IConfigurationStore configuration,
        DurationSetting setting,
        CancellationToken cancellationToken) =>
        (await configuration.ReadAsync(setting, cancellationToken).ConfigureAwait(false))
            .Match(value => value, error => throw new InvalidOperationException(error.Code.ToString()));

    private static string? Carried(HttpRequest request, string cookie) =>
        request.Cookies[cookie] is { Length: > 0 } value ? value : null;

    private static TValue Withheld<TValue>(Error error, ref Error? failure)
    {
        failure = error;

        return default!;
    }
}
