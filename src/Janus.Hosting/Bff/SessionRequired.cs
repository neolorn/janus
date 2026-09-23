using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Janus.Hosting.Bff;

/// <summary>
/// What an endpoint carries to say that it answers nobody.
/// </summary>
/// <remarks>
/// Implements BFF-STEP-001 and API-CONV-003. An endpoint says where it is mounted that
/// it needs a session, so which endpoints need one is read from the mounting rather
/// than from the body of every handler, and the stage that reads it answers them all
/// the same way.
/// </remarks>
internal sealed class SessionRequired
{
    private static readonly SessionRequired Marker = new();

    private SessionRequired()
    {
    }

    /// <summary>
    /// Marks an endpoint that answers nobody.
    /// </summary>
    /// <param name="route">The endpoint, as it is mounted.</param>
    /// <returns>The endpoint, so the caller can go on.</returns>
    /// <exception cref="ArgumentNullException">The endpoint is absent.</exception>
    public static RouteHandlerBuilder On(RouteHandlerBuilder route)
    {
        ArgumentNullException.ThrowIfNull(route);

        return route.WithMetadata(Marker);
    }

    /// <summary>
    /// Whether the endpoint the request reached is one of them.
    /// </summary>
    /// <param name="endpoint">The endpoint, or nothing where routing matched none.</param>
    /// <returns>Whether it needs a session.</returns>
    public static bool Asks(Endpoint? endpoint) =>
        endpoint?.Metadata.GetMetadata<SessionRequired>() is not null;
}
