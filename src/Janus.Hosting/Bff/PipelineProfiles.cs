using System;
using System.Security.Cryptography;
using Janus.Hosting.Callbacks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

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
/// the library names and the callbacks the host mounts on it by path, so neither
/// profile is something an endpoint opts into, and a request the machine profile
/// governs is not governed by the browser one as well.
/// </remarks>
public static class PipelineProfiles
{
    /// <summary>
    /// Mounts the browser profile. Host middleware goes before this call or after the
    /// endpoints, never between the stages.
    /// </summary>
    /// <remarks>
    /// A processor that returns the browser by posting a form from its own site reaches
    /// a public application without the session, the cookie being lax. Such a post is
    /// never carried: the profile answers it 303 with its own address, the browser
    /// reads that address with the session, and the host's route there is a GET that
    /// asks the processor for the outcome rather than reading it from the post
    /// (BFF-CSRF-005). A post of that kind that carries the session is refused as any
    /// cross-site change is.
    /// </remarks>
    /// <param name="application">The host's pipeline.</param>
    /// <returns>The pipeline, for chaining.</returns>
    /// <exception cref="ArgumentNullException">The pipeline is absent.</exception>
    public static IApplicationBuilder UseBrowserProfile(this IApplicationBuilder application)
    {
        ArgumentNullException.ThrowIfNull(application);

        // BFF-MACH-001: a request the machine profile governed carries no cookie, token
        // or header a browser sets, and every stage here would refuse it.
        return application.UseWhen(
            context => context.Features.Get<MachineGoverned>() is null,
            Browser);
    }

    /// <summary>
    /// Mounts the machine profile, which governs the library's own routes that
    /// non-browser callers reach and no others.
    /// </summary>
    /// <param name="application">The host's pipeline.</param>
    /// <returns>The pipeline, for chaining.</returns>
    /// <exception cref="ArgumentNullException">The pipeline is absent.</exception>
    public static IApplicationBuilder UseMachineProfile(this IApplicationBuilder application)
    {
        ArgumentNullException.ThrowIfNull(application);

        // BFF-MACH-001: which routes this profile governs is the library's, so a host
        // mounts the profile and chooses nothing about what it covers.
        return application.UseWhen(
            context => MachineRoutes.Governs(context.Request.Path),
            branch =>
            {
                Machine(branch);
                _ = branch.UseAuthentication();
            });
    }

    /// <summary>
    /// Mounts one of the host's signed callbacks on the machine profile, at the path its
    /// provider calls. Call it before <see cref="UseBrowserProfile"/>; the host maps its
    /// own route at the same path.
    /// </summary>
    /// <param name="application">The host's pipeline.</param>
    /// <param name="path">The path the provider calls, which the host chooses.</param>
    /// <param name="callback">The callback and its provider's scheme.</param>
    /// <returns>The pipeline, for chaining.</returns>
    /// <exception cref="ArgumentNullException">A part is absent.</exception>
    /// <exception cref="ArgumentException">The path or the name is empty.</exception>
    /// <exception cref="CryptographicException">
    /// The algorithm is not one this platform computes a keyed hash with.
    /// </exception>
    public static IApplicationBuilder UseCallback(
        this IApplicationBuilder application,
        PathString path,
        ISignedCallback callback)
    {
        ArgumentNullException.ThrowIfNull(application);
        ArgumentNullException.ThrowIfNull(callback);
        Mountable(path, callback.Name);

        // A scheme this platform cannot compute is found when the host starts, not on
        // the provider's first delivery.
        _ = CryptographicOperations.HmacData(callback.Algorithm, [], []);

        var guard = new SignedCallbackGuard(callback);

        return application.UseWhen(
            context => context.Request.Path.Equals(path, StringComparison.OrdinalIgnoreCase),
            branch =>
            {
                Machine(branch);
                _ = branch.Use(next => context => guard.InvokeAsync(context, next));
            });
    }

    /// <summary>
    /// Mounts one of the host's unsigned callbacks on the machine profile, at the path
    /// its provider calls. Call it before <see cref="UseBrowserProfile"/>; the host maps
    /// its own route at the same path.
    /// </summary>
    /// <param name="application">The host's pipeline.</param>
    /// <param name="path">The path the provider calls, which the host chooses.</param>
    /// <param name="callback">The callback, and how its provider confirms a hint.</param>
    /// <returns>The pipeline, for chaining.</returns>
    /// <exception cref="ArgumentNullException">A part is absent.</exception>
    /// <exception cref="ArgumentException">The path or the name is empty.</exception>
    public static IApplicationBuilder UseCallback(
        this IApplicationBuilder application,
        PathString path,
        IUnsignedCallback callback)
    {
        ArgumentNullException.ThrowIfNull(application);
        ArgumentNullException.ThrowIfNull(callback);
        Mountable(path, callback.Name);

        var guard = new UnsignedCallbackGuard(callback);

        return application.UseWhen(
            context => context.Request.Path.Equals(path, StringComparison.OrdinalIgnoreCase),
            branch =>
            {
                Machine(branch);
                _ = branch.Use(next => context => guard.InvokeAsync(context, next));
            });
    }

    // The stages of the browser profile, in the order the contract fixes.
    private static void Browser(IApplicationBuilder application)
    {
        // BFF-ORDER-001 stage 11, which is last on the way out and therefore first on
        // the way in: a body the reader could not parse fails at the endpoint, after
        // every stage before it has run (API-CONV-002).
        _ = application.UseMiddleware<MalformedRequest>();

        // Stages 2 and 3. The cheap rejections come first, before anything reads the
        // session.
        _ = application.UseMiddleware<ResourceIsolation>();
        _ = application.UseMiddleware<CustomRequestHeader>();
        _ = application.UseMiddleware<OriginValidation>();

        // Stage 5, then stage 6, which needs what stage 5 established. BFF-CSRF-005a:
        // the token's binding target has to exist before the token is checked, so a
        // browser that resolved to neither is given one in between.
        _ = application.UseMiddleware<SessionResolution>();
        _ = application.UseMiddleware<FirstContact>();
        _ = application.UseMiddleware<SynchronizerToken>();

        // Stage 8's floor: an endpoint that answers only a signed-in person is held
        // to one here, after the stages that establish what the browser carries and
        // before anything reads a body (BFF-STEP-001).
        _ = application.UseMiddleware<SessionRequirement>();

        // AUTH-SESS-012: the authorization endpoint is answered here, after the layers
        // that established what the browser carries, because what it issues a code
        // against is the session it found.
        _ = application.UseAuthentication();
    }

    // The stages every route on the machine profile passes, the first of which marks
    // the request as governed here.
    private static void Machine(IApplicationBuilder branch)
    {
        _ = branch.Use((context, next) =>
        {
            context.Features.Set(MachineGoverned.Mark);

            return next(context);
        });
        _ = branch.UseMiddleware<MalformedRequest>();
        _ = branch.UseMiddleware<MachineProfile>();
    }

    private static void Mountable(PathString path, string name)
    {
        if (!path.HasValue || path.Value == "/")
        {
            throw new ArgumentException("A callback is mounted at a path of its own.", nameof(path));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(name);
    }
}
