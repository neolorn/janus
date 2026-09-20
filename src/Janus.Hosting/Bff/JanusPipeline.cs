using System;
using Microsoft.AspNetCore.Builder;

namespace Janus.Hosting.Bff;

/// <summary>
/// The one call a host makes to mount the browser-facing pipeline.
/// </summary>
/// <remarks>
/// Implements BFF-OWN-001, BFF-OWN-003, BFF-CSRF-001, BFF-ORDER-001 and
/// BFF-MACH-001. The stages are mounted here in the order the contract fixes, and a
/// host has no way to put anything between them, to reorder them, or to exclude an
/// endpoint from them: what protects an endpoint is that it is mounted after this
/// call. The machine profile is mounted before the browser one and covers the routes
/// the library names, so neither profile is something an endpoint opts into.
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

        // BFF-ORDER-001 stages 2 and 3. The cheap rejections come first, before
        // anything reads the session.
        _ = application.UseMiddleware<ResourceIsolation>();
        _ = application.UseMiddleware<CustomRequestHeader>();
        _ = application.UseMiddleware<OriginValidation>();

        // Stage 5, then stage 6, which needs what stage 5 established. BFF-CSRF-005a:
        // the token's binding target has to exist before the token is checked, so a
        // browser that resolved to neither is given one in between.
        _ = application.UseMiddleware<SessionResolution>();
        _ = application.UseMiddleware<FirstContact>();
        _ = application.UseMiddleware<SynchronizerToken>();

        // AUTH-SESS-012: the authorization endpoint is answered here, after the layers
        // that established what the browser carries, because what it issues a code
        // against is the session it found.
        _ = application.UseAuthentication();

        return application;
    }

    /// <summary>
    /// Mounts the machine profile, which governs the library's own routes that
    /// non-browser callers reach and no others.
    /// </summary>
    /// <param name="application">The host's pipeline.</param>
    /// <returns>The pipeline, for chaining.</returns>
    /// <exception cref="ArgumentNullException">The pipeline is absent.</exception>
    public static IApplicationBuilder UseJanusMachineProfile(this IApplicationBuilder application)
    {
        ArgumentNullException.ThrowIfNull(application);

        // BFF-MACH-001: which routes this profile governs is the library's, so a host
        // mounts the profile and chooses nothing about what it covers.
        return application.UseWhen(
            context => MachineRoutes.Governs(context.Request.Path),
            branch =>
            {
                _ = branch.UseMiddleware<MachineProfile>();
                _ = branch.UseAuthentication();
            });
    }
}
