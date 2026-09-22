using System;
using System.Text.Json;
using System.Threading.Tasks;
using Janus.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Janus.Hosting.Bff;

/// <summary>
/// The answer to a request whose body the reader could not turn into what the endpoint
/// takes, which never reaches the endpoint to be refused there.
/// </summary>
/// <remarks>
/// Implements API-CONV-002 and BFF-ORDER-001 stage 11. It stands outermost, so the
/// refusal it writes is the last thing to touch the response and every stage inside it
/// answers as it always did. What it says is the shape of the body and nothing of its
/// contents: the member the reader stopped at, never the value it was reading.
/// </remarks>
internal sealed class MalformedRequest(ILogger<MalformedRequest> log) : IMiddleware
{
    /// <summary>
    /// Runs the layer.
    /// </summary>
    /// <param name="context">The request.</param>
    /// <param name="next">The rest of the pipeline.</param>
    /// <returns>The work of carrying or refusing it.</returns>
    /// <exception cref="ArgumentNullException">A part is absent.</exception>
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        try
        {
            await next(context).ConfigureAwait(false);
        }
        catch (BadHttpRequestException unreadable)
        {
            if (context.Response.HasStarted)
            {
                throw;
            }

            string? member = Member(unreadable);

            BrowserProfileLog.BodyUnreadable(log, context.TraceIdentifier, member);
            await Refusal
                .WriteAsync(context, Malformed(member), context.RequestAborted)
                .ConfigureAwait(false);
        }
    }

    // The reader's path names the member it stopped at and nothing of the value it was
    // reading; where it failed before any member, the refusal carries the code alone.
    private static string? Member(BadHttpRequestException unreadable) =>
        unreadable.InnerException is JsonException { Path: { Length: > 0 } member } ? member : null;

    private static Error Malformed(string? member) =>
        member is null
            ? Error.From(ErrorCodes.RequestMalformed)
            : Error.From(
                ErrorCodes.RequestMalformed,
                "member",
                JsonSerializer.SerializeToElement(member));
}
