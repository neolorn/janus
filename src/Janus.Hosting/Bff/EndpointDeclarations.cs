using System;
using Microsoft.AspNetCore.Builder;

namespace Janus.Hosting.Bff;

/// <summary>
/// The one route-builder extension through which an endpoint declares what it answers
/// and what it binds.
/// </summary>
/// <remarks>
/// Implements CONV-DESIGN-006.
/// </remarks>
internal static class EndpointDeclarations
{
    /// <summary>
    /// Adds an endpoint's declaration to its metadata.
    /// </summary>
    /// <param name="route">The endpoint, as it is mounted.</param>
    /// <param name="declaration">What it declares.</param>
    /// <returns>The endpoint, so the caller can go on.</returns>
    /// <exception cref="ArgumentNullException">A part is absent.</exception>
    public static RouteHandlerBuilder Declares(this RouteHandlerBuilder route, EndpointDeclaration declaration)
    {
        ArgumentNullException.ThrowIfNull(route);
        ArgumentNullException.ThrowIfNull(declaration);

        return route.WithMetadata(declaration);
    }
}
