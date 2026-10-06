using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Janus.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Janus.Hosting.Bff;

/// <summary>
/// The answer to a request the framework could not bind to what the endpoint takes, a
/// route or query value that does not read as its type or a body the reader could not
/// turn into the endpoint's, which never reaches the endpoint to be refused there.
/// </summary>
/// <remarks>
/// Implements API-CONV-002, API-CONV-003, CONV-DESIGN-006 and BFF-ORDER-001 stage 11.
/// It stands outermost, so the refusal it writes is the last thing to touch the
/// response and every stage inside it answers as it always did. It names the first
/// value the endpoint declares, in the order declared, that the request's text does
/// not read as; where each reads, the failure is the body's, and what it says is the
/// shape of the body and nothing of its contents: the member the reader stopped at,
/// never the value it was reading.
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

            string? member = EndpointDeclaration.Of(context.GetEndpoint())?.Unread(context.Request)
                ?? Member(unreadable);

            BrowserProfileLog.BodyUnreadable(log, context.TraceIdentifier, member);
            await Refusal
                .WriteAsync(context, Malformed(member), context.RequestAborted)
                .ConfigureAwait(false);
        }
    }

    // The reader's path names the member it stopped at and nothing of the value it was
    // reading; where it failed before any member, the refusal carries the code alone.
    private static string? Member(BadHttpRequestException unreadable) =>
        unreadable.InnerException is JsonException { Path: { Length: > 0 } path } ? Member(path) : null;

    /// <summary>
    /// The member a reader's path stopped at, named as the request writes it.
    /// </summary>
    /// <param name="path">The path, from the <c>$</c> root.</param>
    /// <returns>The member, or nothing where the path names none.</returns>
    /// <remarks>
    /// API-CONV-002 AC4 (D-179): no <c>$</c> root and no list index; a member inside
    /// another by the names from the body's top joined by dots; an element of a list,
    /// or a member inside one, by the list's name; a member inside an element of a body
    /// that is itself a list by the names from that element's top.
    /// </remarks>
    internal static string? Member(string path)
    {
        ArgumentNullException.ThrowIfNull(path);

        var names = new List<string>();
        int at = path.StartsWith('$') ? 1 : 0;
        bool top = true;

        while (at < path.Length)
        {
            if (path[at] == '.')
            {
                int end = path.IndexOfAny(['.', '['], at + 1);
                names.Add(path[(at + 1)..(end < 0 ? path.Length : end)]);
                at = end < 0 ? path.Length : end;
            }
            else if (path[at] == '[' && at + 1 < path.Length && path[at + 1] == '\'')
            {
                int end = path.IndexOf("']", at + 2, StringComparison.Ordinal);

                if (end < 0)
                {
                    break;
                }

                names.Add(path[(at + 2)..end]);
                at = end + 2;
            }
            else if (path[at] == '[' && !top)
            {
                break;
            }
            else if (path[at] == '[')
            {
                int end = path.IndexOf(']', at);
                at = end < 0 ? path.Length : end + 1;
            }
            else
            {
                break;
            }

            top = false;
        }

        return names.Count == 0 ? null : string.Join('.', names);
    }

    private static Error Malformed(string? member) =>
        member is null
            ? Error.From(ErrorCodes.RequestMalformed)
            : Error.From(
                ErrorCodes.RequestMalformed,
                "member",
                JsonSerializer.SerializeToElement(member));
}
