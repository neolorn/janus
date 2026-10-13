using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Janus.Hosting.Bff;

/// <summary>
/// What an endpoint carries to say that an enrolment session reaches it.
/// </summary>
/// <remarks>
/// Implements BFF-ORDER-001 stage 5 and D-189. The routes chapter 09 lists at
/// <c>POST /enrol/begin</c> say so where they are mounted, and the session resolution
/// stage resolves an enrolment session on those and on no other, so a route that does
/// not carry this never learns that the browser holds one.
/// </remarks>
internal sealed class EnrolmentRoute
{
    private static readonly EnrolmentRoute Marker = new();

    private EnrolmentRoute()
    {
    }

    /// <summary>
    /// Marks an endpoint an enrolment session reaches.
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
    /// <returns>Whether an enrolment session is resolved on it.</returns>
    public static bool Is(Endpoint? endpoint) =>
        endpoint?.Metadata.GetMetadata<EnrolmentRoute>() is not null;
}
