using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Logging;

namespace Janus.Hosting.Bff;

/// <summary>
/// The answer to a request the gate refused on a type that conceals its records: the
/// absence of the record, whatever the endpoint wrote.
/// </summary>
/// <param name="refusals">Which refusal the gate concealed, if any.</param>
/// <param name="log">Where the answer is traced to the refusal.</param>
/// <remarks>
/// Implements AUTHZ-CONCEAL-001, AUTHZ-CONCEAL-002, AUTHZ-CONCEAL-004, API-CONV-003,
/// BFF-ERR-003, BFF-ORDER-001 stage 11 and OPS-ENV-002. It stands outermost, so what it
/// answers is the last word on the response. The gate refuses a record the library
/// holds no row for exactly as one the caller may not see, and both are answered here
/// by one writer from what the response carried when the request reached the endpoints,
/// so the two are the same status, headers and bytes but for the identifier. An answer
/// the endpoint had begun before the refusal cannot be taken back, so the connection is
/// closed rather than finished.
/// </remarks>
internal sealed class Concealment(ConcealedRefusals refusals, ILogger<Concealment> log) : IMiddleware
{
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

        IHttpResponseBodyFeature carried = context.Features.GetRequiredFeature<IHttpResponseBodyFeature>();
        await using var withheld = new WithheldBody(carried, refusals);

        context.Features.Set<IHttpResponseBodyFeature>(withheld);

        try
        {
            await next(context).ConfigureAwait(false);
            await withheld.FinishAsync(context.RequestAborted).ConfigureAwait(false);
        }
        finally
        {
            context.Features.Set(carried);
        }

        if (refusals.Correlation is AuditRecordId correlation)
        {
            await AnswerAsync(context, correlation, withheld.Passed, context.RequestAborted)
                .ConfigureAwait(false);
        }
    }

    private async Task AnswerAsync(
        HttpContext context,
        AuditRecordId correlation,
        bool passed,
        CancellationToken cancellationToken)
    {
        if (passed || context.Response.HasStarted)
        {
            BrowserProfileLog.ConcealedTooLate(log, context.TraceIdentifier, correlation.Value);
            context.Abort();

            return;
        }

        context.Response.Clear();
        refusals.Restore(context.Response.Headers);

        BrowserProfileLog.Concealed(log, context.TraceIdentifier, correlation.Value);
        await Refusal
            .WriteAsync(
                context,
                Error.From(
                    ErrorCodes.ResourceNotFound,
                    "correlation",
                    JsonSerializer.SerializeToElement(correlation.Value)),
                cancellationToken)
            .ConfigureAwait(false);
    }
}
