using System;
using System.Text.Json;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Hosting.Bff;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Http;
using OpenIddict.Abstractions;
using OpenIddict.Server;
using OpenIddict.Server.AspNetCore;

namespace Janus.Hosting.Oidc;

/// <summary>
/// The answer to an authorization request the server refused and cannot return to the
/// client, which is given to the browser that carried it.
/// </summary>
/// <remarks>
/// Implements LIB-API-003 AC4, BFF-ERR-001 and AUTH-OIDC-006 AC2. A refusal the client's
/// registered destination can carry goes back to the client as the protocol says. One
/// it cannot, because the request named no client, no pushed request still held or
/// nothing the server could read, stays with the browser, and there the server's own
/// answer is plain text with a description for a developer to read. It is answered by
/// the one writer instead: the protocol's code in the details, the correlation
/// identifier, and no sentence.
/// </remarks>
internal sealed class AuthorizationErrorAnswer
    : IOpenIddictServerHandler<OpenIddictServerEvents.ApplyAuthorizationResponseContext>
{
    private const string Protocol = "error";

    /// <summary>
    /// Where the handler sits: after every answer that returns to the client, and
    /// before the server writes one of its own to the browser.
    /// </summary>
    public static int Order { get; } = OpenIddictServerAspNetCoreHandlers
        .ProcessLocalErrorResponse<OpenIddictServerEvents.ApplyAuthorizationResponseContext>
        .Descriptor.Order - 1;

    /// <inheritdoc/>
    public async ValueTask HandleAsync(OpenIddictServerEvents.ApplyAuthorizationResponseContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Response.Error is not { Length: > 0 } refused
            || !string.IsNullOrEmpty(context.RedirectUri)
            || context.Transaction.GetHttpRequest() is not HttpRequest request)
        {
            return;
        }

        await Refusal
            .WriteAsync(request.HttpContext, Answered(refused), context.CancellationToken)
            .ConfigureAwait(false);

        context.HandleRequest();
    }

    // The server's own fault is a fault like any other; every other refusal it answers
    // to the browser is a request it could not read as one it serves.
    private static Error Answered(string refused) =>
        Error.From(
            refused is OpenIddictConstants.Errors.ServerError ? ErrorCodes.SystemFault : ErrorCodes.RequestMalformed,
            Protocol,
            JsonSerializer.SerializeToElement(refused));
}
