using System;
using Janus.Core;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Janus.Hosting.Bff;

/// <summary>
/// What an endpoint carries to say whether it answers nobody.
/// </summary>
/// <remarks>
/// Implements BFF-STEP-001, BFF-HOST-002 and API-CONV-003 (D-211). An endpoint says where
/// it is mounted whether it needs a session, so which endpoints need one is read from the
/// mounting rather than from the body of every handler, and the stage that reads it
/// answers them all the same way. An endpoint that says nothing needs one: the library
/// marks its own as answering anyone where it mounts them and then marks those that do
/// not, and a host endpoint left unmarked, or an action or a page it reaches through a
/// dynamic route, fails closed.
/// </remarks>
internal sealed class SessionRequired
{
    /// <summary>
    /// The display name ASP.NET Core gives the endpoint it makes for a method a matched
    /// path does not take (<c>HttpMethodMatcherPolicy</c>).
    /// </summary>
    internal const string MethodNotTaken = "405 HTTP Method Not Supported";

    /// <summary>
    /// The display name ASP.NET Core gives the endpoint it makes for a content type a
    /// matched path does not take (<c>AcceptsMatcherPolicy</c>).
    /// </summary>
    internal const string MediaTypeNotTaken = "415 HTTP Unsupported Media Type";

    private static readonly SessionRequired Needed = new(needs: true);

    private static readonly SessionRequired Unneeded = new(needs: false);

    private readonly bool _needs;

    private SessionRequired(bool needs) => _needs = needs;

    /// <summary>
    /// Marks an endpoint that answers nobody.
    /// </summary>
    /// <param name="route">The endpoint, as it is mounted.</param>
    /// <returns>The endpoint, so the caller can go on.</returns>
    /// <exception cref="ArgumentNullException">The endpoint is absent.</exception>
    public static RouteHandlerBuilder On(RouteHandlerBuilder route)
    {
        ArgumentNullException.ThrowIfNull(route);

        return route.WithMetadata(Needed);
    }

    /// <summary>
    /// Marks an endpoint, or every endpoint of a group, that answers anyone. A mark an
    /// endpoint carries itself stands over its group's.
    /// </summary>
    /// <typeparam name="TBuilder">The kind of convention builder.</typeparam>
    /// <param name="builder">The endpoint or group, as it is mounted.</param>
    /// <returns>The builder, so the caller can go on.</returns>
    /// <exception cref="ArgumentNullException">The builder is absent.</exception>
    public static TBuilder Off<TBuilder>(TBuilder builder)
        where TBuilder : IEndpointConventionBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.WithMetadata(Unneeded);
    }

    /// <summary>
    /// Whether the endpoint the request reached is one that needs a session, whatever its
    /// type: a route something mapped and an endpoint a dynamic route resolves to alike.
    /// Only a request that reached none, or one of the endpoints ASP.NET Core's matcher
    /// policies make for a method or a content type a matched path does not take, is held
    /// to none, and is answered as API-CONV-003 gives (D-211).
    /// </summary>
    /// <param name="endpoint">The endpoint, or nothing where routing matched none.</param>
    /// <returns>Whether it needs a session.</returns>
    public static bool Asks(Endpoint? endpoint) =>
        endpoint is not null && !Rejection(endpoint) && Needs(endpoint.Metadata);

    /// <summary>
    /// What stage 8 answers a request to the endpoint with, where the request may not
    /// reach it: the end of the sign-in session it carried, with what has to be done
    /// again, or the code alone where it held none (BFF-STEP-001 AC3).
    /// </summary>
    /// <param name="resolved">What the session resolution stage established.</param>
    /// <param name="endpoint">The endpoint, or nothing where routing matched none.</param>
    /// <returns>The refusal, or nothing where the request may reach the endpoint.</returns>
    /// <exception cref="ArgumentNullException">The resolution is absent.</exception>
    public static Error? Refuses(RequestSession resolved, Endpoint? endpoint)
    {
        ArgumentNullException.ThrowIfNull(resolved);

        return resolved.Live is null && Asks(endpoint)
            ? resolved.Expiry ?? Error.From(ErrorCodes.SessionExpired)
            : null;
    }

    // The mark nearest the endpoint stands: the last its metadata carries, the
    // convention's and the attribute's alike, as a single kind of mark would be read.
    private static bool Needs(EndpointMetadataCollection metadata)
    {
        for (int index = metadata.Count - 1; index >= 0; index--)
        {
            switch (metadata[index])
            {
                case SessionRequired mark:
                    return mark._needs;
                case AllowNoSessionAttribute:
                    return false;
            }
        }

        return true;
    }

    // HttpMethodMatcherPolicy's 405 endpoint and AcceptsMatcherPolicy's 415, as
    // ASP.NET Core 10.0.14 makes them: no metadata, and the display name it gives each.
    // One the framework renamed asks for a session, which fails closed.
    private static bool Rejection(Endpoint endpoint) =>
        endpoint.Metadata.Count == 0
        && endpoint.DisplayName is MethodNotTaken or MediaTypeNotTaken;

}
