using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Net.Http.Headers;

namespace Janus.Hosting.Bff;

/// <summary>
/// How the pipeline answers a request it will not carry further.
/// </summary>
/// <remarks>
/// Implements API-CONV-002, API-CONV-003, API-CONV-004, BFF-CSRF-001, BFF-ERR-001,
/// BFF-ERR-002, BFF-ERR-003, BFF-LOG-001 and CONV-LOG-002. One writer answers every
/// refusal, so a body is the same shape whichever stage produced it and a concealed
/// denial is the same bytes as a genuine absence. A fault answers as a fault and
/// carries nothing but the correlation identifier, whatever the code behind it was.
/// Every refusal it writes is logged under that identifier, and a fault with what the
/// answer withheld.
/// </remarks>
internal static class Refusal
{
    private static readonly IReadOnlyDictionary<string, JsonElement> Nothing =
        new Dictionary<string, JsonElement>(StringComparer.Ordinal);

    /// <summary>
    /// Answers with the status the code carries and the request's correlation
    /// identifier.
    /// </summary>
    /// <param name="context">The request.</param>
    /// <param name="code">What was refused.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of writing it.</returns>
    /// <exception cref="ArgumentNullException">The context is absent.</exception>
    public static Task WriteAsync(
        HttpContext context,
        ErrorCode code,
        CancellationToken cancellationToken) =>
        WriteAsync(context, Error.From(code), cancellationToken);

    /// <summary>
    /// Answers with the status the failure carries, its own structured context, and
    /// the request's correlation identifier.
    /// </summary>
    /// <param name="context">The request.</param>
    /// <param name="error">What was refused.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of writing it.</returns>
    /// <exception cref="ArgumentNullException">A part is absent.</exception>
    public static async Task WriteAsync(
        HttpContext context,
        Error error,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(error);

        int status = ApiStatus.Of(error.Code);
        bool fault = status is StatusCodes.Status500InternalServerError;

        Logged(context, error, fault);

        context.Response.StatusCode = status;
        context.Response.ContentType = "application/json; charset=utf-8";

        if (status is StatusCodes.Status429TooManyRequests)
        {
            Wait(context, error);
        }

        await context.Response
            .WriteAsJsonAsync(
                new ApiError(
                    fault ? ErrorCodes.SystemFault.ToString() : error.Code.ToString(),
                    context.TraceIdentifier,
                    fault ? Nothing : error.Details),
                ApiErrorJson.Default.ApiError,
                contentType: null,
                cancellationToken)
            .ConfigureAwait(false);
    }

    // BFF-LOG-001, BFF-ERR-002: every refusal is logged under the identifier its answer
    // carries, and a fault with the code and the context the answer withholds, so what
    // the answer discards is still retrievable by that identifier. The details are the
    // failure's own structured context, which never holds a sentence or a secret
    // (API-CONV-002, CONV-LOG-003); anything else refused is logged by its code alone.
    private static void Logged(HttpContext context, Error error, bool fault)
    {
        ILogger log = context.RequestServices
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger(typeof(Refusal));

        if (fault)
        {
            BrowserProfileLog.Faulted(log, context.TraceIdentifier, error.Code, error.Details);

            return;
        }

        BrowserProfileLog.Refused(log, context.TraceIdentifier, error.Code);
    }

    // API-CONV-003: a throttled answer carries the interval, and the interval the
    // library knows is the instant the bucket lifts (API-CONV-002, BFF-ABUSE-001).
    private static void Wait(HttpContext context, Error error)
    {
        if (!error.Details.TryGetValue("retryAt", out JsonElement at)
            || !at.TryGetDateTimeOffset(out DateTimeOffset lifts))
        {
            return;
        }

        TimeProvider time = context.RequestServices.GetRequiredService<TimeProvider>();
        TimeSpan remaining = lifts - time.GetUtcNow();
        long seconds = remaining > TimeSpan.Zero ? (long)Math.Ceiling(remaining.TotalSeconds) : 0;

        context.Response.Headers[HeaderNames.RetryAfter] =
            seconds.ToString(CultureInfo.InvariantCulture);
    }
}
