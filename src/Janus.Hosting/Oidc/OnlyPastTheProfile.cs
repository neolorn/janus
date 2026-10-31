using System;
using System.Threading.Tasks;
using Janus.Hosting.Bff;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Http;
using OpenIddict.Server;

namespace Janus.Hosting.Oidc;

/// <summary>
/// The provider's first step on every request: it passes on a request that its profile
/// has not passed, untouched.
/// </summary>
/// <remarks>
/// Implements BFF-ORDER-001 AC8 (D-230). The framework's own authentication middleware,
/// which it adds ahead of the host's middleware wherever the host calls none, hands
/// every request to the provider before any stage has run. Only the authentication the
/// profiles mount inside the branch the host mounted the library in finds the profile's
/// mark, so a request a stage refuses reaches no provider endpoint, and the
/// authorization endpoint finds the session the stages resolved.
/// </remarks>
internal sealed class OnlyPastTheProfile : IOpenIddictServerHandler<OpenIddictServerEvents.ProcessRequestContext>
{
    /// <summary>
    /// Where the handler sits: before every step of the server's.
    /// </summary>
    public const int Order = int.MinValue;

    /// <inheritdoc/>
    public ValueTask HandleAsync(OpenIddictServerEvents.ProcessRequestContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Transaction.GetHttpRequest() is not HttpRequest request
            || request.HttpContext.Features.Get<ProfilePassed>() is null)
        {
            context.SkipRequest();
        }

        return ValueTask.CompletedTask;
    }
}
