using System;
using System.Threading.Tasks;
using OpenIddict.Server;

namespace Janus.Hosting.Oidc;

/// <summary>
/// The server's check of an authorization request's reference, which requires one of
/// every request the deployment pushes and admits the continuation of a pending
/// authorization without one.
/// </summary>
/// <remarks>
/// Implements AUTH-OIDC-006 AC2 and AUTH-SESS-012 AC10 (D-224). The continuation carries
/// no reference because its request was read from a reference already, which was spent
/// when the request was kept; what it answers is that request, read back off the session
/// record. Every other request is held to the server's own check.
/// </remarks>
internal sealed class ReferenceOrContinued
    : IOpenIddictServerHandler<OpenIddictServerEvents.ValidateAuthorizationRequestContext>
{
    private static readonly OpenIddictServerHandlers.Authentication.ValidateRequestUriParameter Checked = new();

    /// <inheritdoc/>
    public ValueTask HandleAsync(OpenIddictServerEvents.ValidateAuthorizationRequestContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return AuthorizationContinued.Continues(context.Transaction)
            ? ValueTask.CompletedTask
            : Checked.HandleAsync(context);
    }
}
