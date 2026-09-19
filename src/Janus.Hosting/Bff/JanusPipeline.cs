using System;
using Microsoft.AspNetCore.Builder;

namespace Janus.Hosting.Bff;

/// <summary>
/// The one call a host makes to mount the browser-facing pipeline.
/// </summary>
/// <remarks>
/// Implements BFF-OWN-001, BFF-OWN-003, BFF-CSRF-001 and BFF-ORDER-001. The stages
/// are mounted here in the order the contract fixes, and a host has no way to put
/// anything between them, to reorder them, or to exclude an endpoint from them: what
/// protects an endpoint is that it is mounted after this call.
/// </remarks>
public static class JanusPipeline
{
    /// <summary>
    /// Mounts the browser profile. Host middleware goes before this call or after the
    /// endpoints, never between the stages.
    /// </summary>
    /// <param name="application">The host's pipeline.</param>
    /// <returns>The pipeline, for chaining.</returns>
    /// <exception cref="ArgumentNullException">The pipeline is absent.</exception>
    public static IApplicationBuilder UseJanusBrowserProfile(this IApplicationBuilder application)
    {
        ArgumentNullException.ThrowIfNull(application);

        // BFF-ORDER-001 stages 2, 3 and 6. The cheap rejections come first, before
        // anything reads the session; the token, which needs it, comes after.
        _ = application.UseMiddleware<ResourceIsolation>();
        _ = application.UseMiddleware<CustomRequestHeader>();
        _ = application.UseMiddleware<OriginValidation>();
        _ = application.UseMiddleware<SynchronizerToken>();

        return application;
    }
}
