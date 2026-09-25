using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Janus.Hosting.Bff;

/// <summary>
/// The answer to a request nothing under the mount answered as the library answers:
/// a fault that escaped a stage or an endpoint, a path no endpoint matched, and a path
/// matched under another method.
/// </summary>
/// <param name="refusals">What the response carried when the request reached the endpoints.</param>
/// <remarks>
/// Implements LIB-API-003 AC4, BFF-ERR-001, BFF-ERR-002 and BFF-ORDER-001 stage 11. The
/// framework's own answers to these are a bare status or, where the host turns its
/// error pages on, a page for a person to read; both are replaced here by the one
/// writer's body. A fault answers <c>system.fault</c> with the correlation identifier
/// and nothing else, and the type of what was thrown is all that is kept of it, in the
/// log the writer keeps under that identifier, because a message can carry a value
/// (CONV-LOG-003). A path matched under another method answers as a path matched under
/// none, since the methods a path takes are not something a refusal discloses. An
/// answer that has begun is not replaced, and a request the caller abandoned is not
/// answered.
/// </remarks>
internal sealed class ErrorTranslation(ConcealedRefusals refusals) : IMiddleware
{
    private const string Fault = "fault";

    /// <summary>
    /// Runs the layer.
    /// </summary>
    /// <param name="context">The request.</param>
    /// <param name="next">The rest of the pipeline.</param>
    /// <returns>The work of carrying or answering it.</returns>
    /// <exception cref="ArgumentNullException">A part is absent.</exception>
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        Error? fault = (await ContainedAsync(context, next, context.RequestAborted).ConfigureAwait(false))
            .Match(() => (Error?)null, error => error);

        if (fault is not null)
        {
            await AnswerAsync(context, fault).ConfigureAwait(false);
        }
        else if (Unanswered(context))
        {
            await AnswerAsync(context, Error.From(ErrorCodes.ResourceNotFound)).ConfigureAwait(false);
        }
    }

    // BFF-ERR-002: what escaped becomes the request's failure, as a job's fault becomes
    // the job's (INF-BG-001), by its type alone. A cancellation is the caller going
    // away only when the caller has gone, which the server already knows and nobody is
    // left to read; one anything else threw is a fault like any other.
    private static async ValueTask<Result> ContainedAsync(
        HttpContext context,
        RequestDelegate next,
        CancellationToken cancellationToken)
    {
        try
        {
            await next(context).ConfigureAwait(false);

            return Result.Success();
        }
        catch (Exception fault)
            when (!context.Response.HasStarted
                && (fault is not OperationCanceledException || !cancellationToken.IsCancellationRequested))
        {
            return Result.Failure(Error.From(
                ErrorCodes.SystemFault,
                Fault,
                JsonSerializer.SerializeToElement(fault.GetType().Name)));
        }
    }

    // What the stages wrote stays, as it does for a concealed refusal, and nothing
    // written after them does: neither what a faulting endpoint had set nor the methods
    // routing named for a path.
    private Task AnswerAsync(HttpContext context, Error error)
    {
        context.Response.Clear();
        refusals.Restore(context.Response.Headers);

        return Refusal.WriteAsync(context, error, context.RequestAborted);
    }

    // The framework's own answers, which write a status and nothing else: no endpoint
    // matched the path, or routing matched it under another method and stood in an
    // endpoint of its own, which is not a route, to refuse it.
    private static bool Unanswered(HttpContext context) =>
        !context.Response.HasStarted
        && context.Response.ContentType is null
        && context.Response.StatusCode switch
        {
            StatusCodes.Status404NotFound => context.GetEndpoint() is null,
            StatusCodes.Status405MethodNotAllowed => context.GetEndpoint() is not RouteEndpoint,
            _ => false,
        };
}
