using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;
using Microsoft.AspNetCore.Http;

namespace Janus.Hosting.Bff;

/// <summary>
/// How the pipeline answers a request it will not carry further.
/// </summary>
/// <remarks>
/// Implements API-CONV-002, API-CONV-004 and BFF-CSRF-001. Every layer of section 4
/// answers with the one code <c>10</c> gives the layer, so which of them fired is not
/// something the answer discloses.
/// </remarks>
internal static class Refusal
{
    private static readonly IReadOnlyDictionary<string, string> Nothing =
        new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>
    /// Answers 403 with the code and the request's correlation identifier.
    /// </summary>
    /// <param name="context">The request.</param>
    /// <param name="code">What was refused.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of writing it.</returns>
    /// <exception cref="ArgumentNullException">The context is absent.</exception>
    public static async Task WriteAsync(
        HttpContext context,
        ErrorCode code,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        context.Response.ContentType = "application/json; charset=utf-8";

        await context.Response
            .WriteAsJsonAsync(
                new ApiError(code.ToString(), context.TraceIdentifier, Nothing),
                ApiErrorJson.Default.ApiError,
                contentType: null,
                cancellationToken)
            .ConfigureAwait(false);
    }
}
