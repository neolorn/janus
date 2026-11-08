using System;
using Janus.Authorization.Grants;
using Janus.Core;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Janus.Hosting.Authorization;

/// <summary>
/// Which way a removal of access came, read from the endpoint the request reached: one
/// of the library's own routes is the administration, and anything else, a host's own
/// endpoint or work no request carries, called the library directly.
/// </summary>
/// <param name="requests">Where the request being served is read.</param>
/// <remarks>
/// Implements AUTHZ-GRANT-005 (D-281). The library marks its routes where it mounts them,
/// so the endpoint itself says which way it came and no endpoint calls past the one
/// contract operation it serves (LIB-API-005).
/// </remarks>
internal sealed class RequestRemovalPaths(IHttpContextAccessor requests) : IRemovalPaths
{
    private static readonly Mounted Library = new();

    /// <inheritdoc/>
    public AccessRemovalPath Current =>
        requests.HttpContext?.GetEndpoint()?.Metadata.GetMetadata<Mounted>() is null
            ? AccessRemovalPath.Library
            : AccessRemovalPath.Administration;

    /// <summary>
    /// Marks every endpoint of a group as one of the library's own routes.
    /// </summary>
    /// <typeparam name="TBuilder">The kind of convention builder.</typeparam>
    /// <param name="builder">The group, as it is mounted.</param>
    /// <returns>The builder, so the caller can go on.</returns>
    /// <exception cref="ArgumentNullException">The builder is absent.</exception>
    public static TBuilder Mark<TBuilder>(TBuilder builder)
        where TBuilder : IEndpointConventionBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.WithMetadata(Library);
    }

    // What one of the library's own routes carries.
    private sealed class Mounted;
}
