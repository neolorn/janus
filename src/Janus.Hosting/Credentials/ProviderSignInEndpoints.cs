using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Credentials;
using Janus.Core;
using Janus.Hosting.Bff;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Janus.Hosting.Credentials;

/// <summary>
/// The routes a sign-in, a registration or a link at a social provider passes
/// through, and the two that link and unlink an identity at one.
/// </summary>
/// <remarks>
/// Implements IDN-LIFE-012, REG-IDENT-008, BFF-CSRF-005a, BFF-MACH-001 and chapter 09
/// section 4 <c>POST</c> and <c>DELETE /account/link/{provider}</c>. The start and the
/// continuation are safe methods reached as top-level navigations, so neither carries
/// a synchronizer token; what stands in its place is the state the browser was sent
/// out with, bound to what the browser carries. The return a provider sends the
/// browser to is on the machine profile, because a provider that returns by a posted
/// form posts across sites: it reads nothing of the browser and sends it on, by a
/// read, to the continuation.
/// </remarks>
internal static class ProviderSignInEndpoints
{
    private const string Start = "/auth/providers/";

    private const string Return = "/callbacks/providers/";

    private const string Link = "/account/link/";

    private const string Returned = "/return";

    private static readonly IResult Nothing = TypedResults.NoContent();

    // What the return carries on to the continuation, which is what the provider
    // answers with and nothing else it sent.
    private static readonly string[] Carried = ["code", "state", "error"];

    /// <summary>
    /// Mounts them.
    /// </summary>
    /// <param name="endpoints">Where the host is mounting the library.</param>
    /// <returns>The builder, so the caller can go on.</returns>
    /// <exception cref="ArgumentNullException">The route builder is absent.</exception>
    public static IEndpointRouteBuilder MapProviderSignIn(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        foreach ((string route, Factor provider) in ProviderRoutes.Named)
        {
            _ = endpoints.MapGet(
                Start + route,
                (ProviderSignIn signIn,
                    HttpContext context,
                    string? intent,
                    string? returnTo,
                    CancellationToken cancellationToken) =>
                    signIn.StartAsync(context, provider, intent, returnTo, cancellationToken));

            _ = endpoints.MapGet(
                Start + route + Returned,
                (ProviderSignIn signIn,
                    HttpContext context,
                    [NeverLogged] string? code,
                    string? state,
                    string? error,
                    CancellationToken cancellationToken) =>
                    signIn.ReturnAsync(context, provider, code, state, error, cancellationToken));

            _ = endpoints.MapMethods(
                Return + route + Returned,
                [HttpMethods.Get, HttpMethods.Post],
                (HttpContext context, CancellationToken cancellationToken) =>
                    ForwardAsync(context, route, cancellationToken));

            _ = SessionRequired.On(endpoints.MapPost(
                Link + route,
                (ICredentials credentials, RequestSession browser, CancellationToken cancellationToken) =>
                    LinkableAsync(credentials, browser, provider, cancellationToken)));

            _ = SessionRequired.On(endpoints.MapDelete(
                Link + route,
                (ICredentials credentials,
                    RequestSession browser,
                    HttpContext context,
                    CancellationToken cancellationToken) =>
                    UnlinkAsync(credentials, browser, context, provider, cancellationToken)));
        }

        return endpoints;
    }

    // BFF-MACH-001: the browser a provider returns is sent on, by a read, to the
    // continuation under the same mount, carrying what the provider answered with.
    private static async Task ForwardAsync(
        HttpContext context,
        string route,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        IFormCollection? form = HttpMethods.IsPost(context.Request.Method)
            && context.Request.HasFormContentType
                ? await context.Request.ReadFormAsync(cancellationToken).ConfigureAwait(false)
                : null;

        var forwarded = new List<KeyValuePair<string, string?>>();

        foreach (string name in Carried)
        {
            if ((form is null ? context.Request.Query[name] : form[name]) is { Count: 1 } values
                && values.ToString() is { Length: > 0 } value)
            {
                forwarded.Add(new(name, value));
            }
        }

        context.Response.StatusCode = StatusCodes.Status303SeeOther;
        context.Response.Headers.Location = context.Request.PathBase
            .Add(new PathString(Start + route + Returned))
            .Add(QueryString.Create(forwarded));
    }

    // IDN-LIFE-012, chapter 10 section 5a: whether the session may link now, which
    // the start of the round trip asks again, so a browser learns it must step up
    // before it leaves for the provider rather than after it returns.
    private static async Task<IResult> LinkableAsync(
        ICredentials credentials,
        RequestSession browser,
        Factor provider,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(credentials);
        ArgumentNullException.ThrowIfNull(browser);

        return Answers.Of(
            await credentials
                .LinkableAsync(Acting(browser), provider, cancellationToken)
                .ConfigureAwait(false),
            Nothing);
    }

    private static async Task<IResult> UnlinkAsync(
        ICredentials credentials,
        RequestSession browser,
        HttpContext context,
        Factor provider,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(credentials);
        ArgumentNullException.ThrowIfNull(browser);
        ArgumentNullException.ThrowIfNull(context);

        return Answers.Of(
            await credentials
                .UnlinkAsync(
                    Acting(browser),
                    provider,
                    RequestOrigin.Source(context.Request),
                    cancellationToken)
                .ConfigureAwait(false),
            Nothing);
    }

    // BFF-STEP-001: both are mounted as endpoints that need a session, so the stage
    // that requires one has already answered a request that arrived without it.
    private static CredentialAuthority Acting(RequestSession browser) =>
        CredentialAuthority.Of(AccessContext.Of(browser.Required.Subject), browser.Required.Id);
}
