using System.Threading.Tasks;
using Janus.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpLogging;

namespace Janus.Hosting.Bff;

/// <summary>
/// Takes the bodies out of what the framework's request logging records, for every
/// request it cannot show to be outside a marked endpoint.
/// </summary>
/// <remarks>
/// Implements BFF-LOG-002, CONV-LOG-003 and entry 274. The logging and its fields are
/// the host's; this runs inside it on every request, after any per-endpoint setting
/// has been applied, so no setting turns a marked body back on. A request whose
/// endpoint is not known when the logging reads it, because routing has not run yet or
/// because the provider answers it without one, is treated as marked: body logging
/// needs an endpoint that is known and unmarked.
/// </remarks>
internal sealed class SensitiveBodyLogging : IHttpLoggingInterceptor
{
    private const HttpLoggingFields Bodies = HttpLoggingFields.RequestBody | HttpLoggingFields.ResponseBody;

    /// <inheritdoc/>
    public ValueTask OnRequestAsync(HttpLoggingInterceptorContext logContext)
    {
        Seal(logContext);

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask OnResponseAsync(HttpLoggingInterceptorContext logContext)
    {
        Seal(logContext);

        return ValueTask.CompletedTask;
    }

    private static void Seal(HttpLoggingInterceptorContext logContext)
    {
        if (logContext.HttpContext.GetEndpoint() is not Endpoint endpoint
            || endpoint.Metadata.GetMetadata<SensitiveBodyAttribute>() is not null)
        {
            logContext.Disable(Bodies);
        }
    }
}
