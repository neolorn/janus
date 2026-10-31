using System;
using System.Threading.Tasks;
using OpenIddict.Abstractions;
using OpenIddict.Server;

namespace Janus.Hosting.Oidc;

/// <summary>
/// The requirement a client may be registered with that its authorization requests be
/// pushed, which admits the continuation of a pending authorization without a reference.
/// </summary>
/// <param name="applications">What the server's own requirement reads the client from.</param>
/// <remarks>
/// Implements AUTH-OIDC-006 AC2 and AUTH-SESS-012 AC10 (D-224). The continuation carries
/// no reference because its request was read from a reference already, which was spent
/// when the request was kept; what it answers is that request, read back off the session
/// record. Every other request is held to the server's own requirement.
/// </remarks>
internal sealed class PushedOrContinued(IOpenIddictApplicationManager applications)
    : IOpenIddictServerHandler<OpenIddictServerEvents.ValidateAuthorizationRequestContext>
{
    /// <inheritdoc/>
    public ValueTask HandleAsync(OpenIddictServerEvents.ValidateAuthorizationRequestContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return AuthorizationContinued.Continues(context.Transaction)
            ? ValueTask.CompletedTask
            : new OpenIddictServerHandlers.Authentication.ValidatePushedAuthorizationRequestsRequirement(applications)
                .HandleAsync(context);
    }
}
