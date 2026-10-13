using System;
using System.Threading.Tasks;
using OpenIddict.Server;
using OpenIddict.Server.AspNetCore;

namespace Janus.Hosting.Oidc;

/// <summary>
/// An error the provider answers, which carries the protocol's code alone.
/// </summary>
/// <typeparam name="TContext">The answer of one endpoint the provider serves.</typeparam>
/// <remarks>
/// Implements LIB-API-003 AC1. The server writes a sentence and a documentation address
/// beside every code it refuses with; RFC 6749 section 5.2 makes both optional and the
/// library writes no sentence, so both are taken off before anything writes the body,
/// the <c>WWW-Authenticate</c> header or the address the client is sent back to.
/// </remarks>
internal sealed class ProtocolErrorAlone<TContext> : IOpenIddictServerHandler<TContext>
    where TContext : OpenIddictServerEvents.BaseRequestContext
{
    /// <summary>
    /// Where the handler sits: before the first step that writes the answer.
    /// </summary>
    public static int Order { get; } = OpenIddictServerAspNetCoreHandlers
        .AttachHttpResponseCode<OpenIddictServerEvents.ApplyTokenResponseContext>
        .Descriptor.Order - 1;

    /// <inheritdoc/>
    public ValueTask HandleAsync(TContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Transaction.Response is { Error.Length: > 0 } refused)
        {
            refused.ErrorDescription = null;
            refused.ErrorUri = null;
        }

        return ValueTask.CompletedTask;
    }
}
