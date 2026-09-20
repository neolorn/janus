using System;
using System.Threading.Tasks;
using Janus.Core;
using Microsoft.AspNetCore.Http;

namespace Janus.Hosting.Bff;

/// <summary>
/// A failure, as an endpoint returns it.
/// </summary>
/// <param name="error">What was refused.</param>
/// <remarks>
/// Implements API-CONV-002, API-CONV-003 and BFF-ERR-003. Every endpoint hands its
/// failure back the same way and none of them writes a response, so the status, the
/// body and the concealment are the pipeline's and not each endpoint's to remember.
/// </remarks>
internal sealed class RefusedAnswer(Error error) : IResult
{
    /// <summary>
    /// Writes the refusal.
    /// </summary>
    /// <param name="httpContext">The request.</param>
    /// <returns>The work of writing it.</returns>
    /// <exception cref="ArgumentNullException">The request is absent.</exception>
    public Task ExecuteAsync(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        return Refusal.WriteAsync(httpContext, error, httpContext.RequestAborted);
    }
}
