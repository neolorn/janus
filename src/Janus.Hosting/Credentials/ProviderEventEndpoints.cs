using System;
using Janus.Core;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Janus.Hosting.Credentials;

/// <summary>
/// The social providers' security events, <c>POST /callbacks/providers/{provider}</c>,
/// on the machine profile.
/// </summary>
/// <remarks>
/// Implements IDN-LIFE-012a AC3, BFF-MACH-001 and INT-GEN-003. One route a provider the
/// factor catalogue names as social, so a path naming any other is not the library's.
/// </remarks>
internal static class ProviderEventEndpoints
{
    private const string Prefix = "/callbacks/providers/";

    /// <summary>
    /// Mounts them.
    /// </summary>
    /// <param name="endpoints">Where the host is mounting the library.</param>
    /// <returns>The builder, so the caller can go on.</returns>
    /// <exception cref="ArgumentNullException">The route builder is absent.</exception>
    public static IEndpointRouteBuilder MapProviderEvents(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        foreach ((string route, Factor provider) in ProviderRoutes.Named)
        {
            _ = endpoints.MapPost(
                Prefix + route,
                context => context.RequestServices
                    .GetRequiredService<ProviderEventIntake>()
                    .TakeAsync(context, provider, context.RequestAborted));
        }

        return endpoints;
    }
}
