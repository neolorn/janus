using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Janus.Hosting.Bff;

/// <summary>
/// What an endpoint carries to say whether it answers nobody.
/// </summary>
/// <remarks>
/// Implements BFF-STEP-001, BFF-HOST-002 and API-CONV-003. An endpoint says where it is
/// mounted whether it needs a session, so which endpoints need one is read from the
/// mounting rather than from the body of every handler, and the stage that reads it
/// answers them all the same way. An endpoint that says nothing needs one: the library
/// marks its own as answering anyone where it mounts them and then marks those that do
/// not, and a host endpoint left unmarked fails closed.
/// </remarks>
internal sealed class SessionRequired
{
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
    /// Whether the endpoint the request reached is one that needs a session. Only an
    /// endpoint something mapped is held to one: a request that reached none, or only
    /// the framework's answer to a method a mapped path does not take, is answered as
    /// no path is (API-CONV-003).
    /// </summary>
    /// <param name="endpoint">The endpoint, or nothing where routing matched none.</param>
    /// <returns>Whether it needs a session.</returns>
    public static bool Asks(Endpoint? endpoint) =>
        endpoint is RouteEndpoint && (endpoint.Metadata.GetMetadata<SessionRequired>()?._needs ?? true);
}
