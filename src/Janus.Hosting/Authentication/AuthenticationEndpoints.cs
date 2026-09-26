using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication;
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

        _ = group.MapPost("/begin", BeginAsync);
        _ = group.MapPost("/factor", PresentAsync);
        _ = group.MapPost("/device/verify", VerifyDeviceAsync);
        _ = SessionRequired.On(group.MapPost("/step-up", StepUpAsync));
        _ = group.MapPost("/link", LinkAsync);
        _ = group.MapPost("/link/abandon", AbandonLinkAsync);
        _ = group.MapPost("/email-otp", CodeAsync);
        _ = SessionRequired.On(group.MapPost("/logout", LogoutAsync));
        _ = SessionRequired.On(group.MapGet("/session", SessionAsync));

        // Chapter 09 section 3 lists the browsers the account knows under the account
        // and the sessions elsewhere, so the two lists are never read as one
        // (AUTH-SESS-013).
        RouteGroupBuilder devices = endpoints.MapGroup("/account/devices");

        _ = SessionRequired.On(devices.MapGet("/", ListDevicesAsync));
        _ = SessionRequired.On(devices.MapDelete("/{id:guid}", ForgetDeviceAsync));

        return endpoints;
    }

    // AUTH-ABUSE-001 AC5: the browser's own tokens travel with the sign-in it opens, so
    // a browser the account knows is not held by an attack on the account.
    private static async Task<IResult> BeginAsync(
        SignInRequest request,
        IAuthentication authentication,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(authentication);
        ArgumentNullException.ThrowIfNull(context);

        return request.Identifier is not { Length: > 0 } identifier
            ? Answers.Malformed("identifier")
            : Answers.Of(
                await authentication
                    .BeginAsync(
                        identifier,
                        RequestOrigin.Source(context.Request),
                        Carried(context.Request, BrowserCookies.Browser),
                        Carried(context.Request, BrowserCookies.Device),
                        cancellationToken)
                    .ConfigureAwait(false),
                challenge => TypedResults.Json(
                    SignInChallengeView.Of(challenge),
                    AuthenticationJson.Default.SignInChallengeView,
                    contentType: null,
                    StatusCodes.Status200OK));
    }

    // REG-SESS-003, API-LAND-001: a link completes in the browser that asked for it
    // and on a press; the same call from anywhere else changes nothing and carries
    // back the code to type where the sign-in began.
    private static async Task<IResult> PresentAsync(
        PresentFactorRequest request,
        AuthenticationService authentication,
        BrowserSessionCookies cookies,
        IConfigurationStore configuration,
        TimeProvider time,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(authentication);
        ArgumentNullException.ThrowIfNull(context);

        if (request.ChallengeId is not { Length: > 0 } challenge)
        {
            return Answers.Malformed("challengeId");
        }

        SessionOrigin origin = RequestOrigin.Of(context.Request);

        if (request.LinkToken is { Length: > 0 } token)
        {
            return await LandedAsync(
                    await authentication
                        .LandAsync(
                            challenge,
                            Carried(context.Request, BrowserCookies.PreAuthentication),
                            token,
                            request.Press,
                            origin,
                            Carried(context.Request, BrowserCookies.Browser),
                            cancellationToken)
                        .ConfigureAwait(false),
                    cookies,
                    configuration,
                    time,
                    context,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        return await ReachedAsync(
                await authentication
                    .PresentAsync(
                        challenge,
                        Presented(request),
                        origin,
                        Carried(context.Request, BrowserCookies.Browser),
                        Carried(context.Request, BrowserCookies.Device),
                        cancellationToken)
                    .ConfigureAwait(false),
                cookies,
                configuration,
                time,
                context,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static async Task<IResult> VerifyDeviceAsync(
        VerifyDeviceRequest request,
        AuthenticationService authentication,
        BrowserSessionCookies cookies,
        IConfigurationStore configuration,
        TimeProvider time,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(authentication);
        ArgumentNullException.ThrowIfNull(context);

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
                configuration,
                time,
                context,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static async Task<IResult> StepUpAsync(
        PresentFactorRequest request,
        AuthenticationService authentication,
        RequestSession browser,
        BrowserSessionCookies cookies,
        IConfigurationStore configuration,
        TimeProvider time,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(authentication);
        ArgumentNullException.ThrowIfNull(browser);
        ArgumentNullException.ThrowIfNull(context);

        AccessContext holder = Asking(browser);

        if (request.ChallengeId is not { Length: > 0 } challenge)
        {
            return Answers.Malformed("challengeId");
        }

        return await ReachedAsync(
                await authentication
                    .RaiseAsync(
                        holder,
                        browser.Required.Id,
                        challenge,
                        Presented(request),
                        RequestOrigin.Source(context.Request),
                        cancellationToken)
                    .ConfigureAwait(false),
                cookies,
                configuration,
                time,
                context,
                cancellationToken)
            .ConfigureAwait(false);
    }

    // AUTH-ABUSE-003: accepted whether or not the identifier belongs to an account
    // and whether or not the policy enables the entry the link would be.
    private static async Task<IResult> LinkAsync(
        SignInRequest request,
        IAuthentication authentication,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(authentication);
        ArgumentNullException.ThrowIfNull(context);

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
        SignInRequest request,
        IAuthentication authentication,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(authentication);
        ArgumentNullException.ThrowIfNull(context);

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
        AbandonLinkRequest request,
        IAuthentication authentication,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(authentication);

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
        Guid id,
        IAuthentication authentication,
        RequestSession browser,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(authentication);
        ArgumentNullException.ThrowIfNull(browser);

        AccessContext holder = Asking(browser);

        return Answers.Of(
            await authentication
                .ForgetDeviceAsync(holder, new DeviceId(id), cancellationToken)
                .ConfigureAwait(false),
            Nothing);
    }

    private static FactorPresentation Presented(PresentFactorRequest request) =>
        new(request.Factor)
        {
            Value = request.Value,
            Assertion = request.Assertion,
            TrustDevice = request.TrustDevice,
        };

    private static async Task<IResult> LandedAsync(
        Result<LandedSignIn> outcome,
        BrowserSessionCookies cookies,
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

        return await CarriedAsync(signedIn, cookies, configuration, time, context, cancellationToken)
            .ConfigureAwait(false);
    }

    private static async Task<IResult> ReachedAsync(
        Result<SignInOutcome> outcome,
        BrowserSessionCookies cookies,
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

        return await CarriedAsync(reached, cookies, configuration, time, context, cancellationToken)
            .ConfigureAwait(false);
    }

    // AUTH-SESS-006, AUTH-FACT-015, AUTH-FACT-016: the session pair is written again
    // by every step that issued one, which is what rotation looks like at the
    // boundary, and each lasting cookie is written only where the step produced one.
    private static async Task<IResult> CarriedAsync(
        SignInOutcome outcome,
        BrowserSessionCookies cookies,
        IConfigurationStore configuration,
        TimeProvider time,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(cookies);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(time);

        if (outcome.Session is IssuedSession issued)
        {
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

        return AccessContext.Of(browser.Required.Subject);
    }

    // How long the browser is to carry what it was just handed. A deployment that has
    // set nothing gets the shipped default, which is what the store answers with.
    private static async ValueTask<TimeSpan> ForAsync(
        IConfigurationStore configuration,
        DurationSetting setting,
        CancellationToken cancellationToken) =>
        (await configuration.ReadAsync(setting, cancellationToken).ConfigureAwait(false))
            .Match(value => value, _ => setting.Default);

    private static string? Carried(HttpRequest request, string cookie) =>
        request.Cookies[cookie] is { Length: > 0 } value ? value : null;

    private static TValue Withheld<TValue>(Error error, ref Error? failure)
    {
        failure = error;

        return default!;
    }
}
