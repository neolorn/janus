using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Sending;
using Janus.Core;
using Janus.Hosting.Bff;
using Janus.Hosting.Callbacks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;

namespace Janus.Hosting.Sending;

/// <summary>
/// The SMS gateway's delivery report, chapter 09 section 10, on the machine profile.
/// </summary>
/// <remarks>
/// Implements INT-SMS-005, INT-GEN-003, BFF-MACH-001, BFF-MACH-003, CONV-DESIGN-006 and
/// CONV-LOG-004. The transport the deployment registers reads the gateway's own
/// parameters; what it read goes to <see cref="DeliveryReports"/>, which can release a
/// send and do nothing else.
/// </remarks>
internal static class DeliveryReportEndpoints
{
    // The name a rejection is logged under, as the route under /callbacks names it.
    private const string Callback = "sms/dlr";

    private const string RetryAt = "retryAt";

    /// <summary>
    /// Mounts it.
    /// </summary>
    /// <param name="endpoints">Where the host is mounting the library.</param>
    /// <returns>The builder, so the caller can go on.</returns>
    /// <exception cref="ArgumentNullException">The route builder is absent.</exception>
    public static IEndpointRouteBuilder MapDeliveryReports(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        _ = endpoints.MapGet("/callbacks/sms/dlr", ReportAsync)
            .Declares(EndpointDeclaration
                .Answering(
                    ErrorCodes.CallbackInProgress, ErrorCodes.CallbackRejected))
            .Produces(StatusCodes.Status200OK);

        return endpoints;
    }

    private static async Task<IResult> ReportAsync(
        HttpRequest request,
        ISmsTransport transport,
        DeliveryReports reports,
        ILogger<DeliveryReports> log,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(transport);
        ArgumentNullException.ThrowIfNull(reports);
        ArgumentNullException.ThrowIfNull(log);

        // A report the transport cannot read carries no reference, and is rejected
        // and counted as a forged one is.
        SmsDeliveryReport? report = transport
            .ReadReport(Parameters(request.Query))
            .Match<SmsDeliveryReport?>(value => value, _ => null);

        Result outcome = await reports
            .ReportAsync(
                RequestOrigin.Source(request),
                report?.Reference,
                report?.Delivered is true,
                cancellationToken)
            .ConfigureAwait(false);

        // INT-GEN-003 AC3 (D-207): the route logs each rejection as every other callback
        // route does, naming the check and never the source; the rate limit's refusal is
        // the one that carries the instant to retry at.
        if (outcome.Match<Error?>(() => null, error => error) is Error refused
            && refused.Code == ErrorCodes.CallbackRejected)
        {
            CallbackLog.Refused(
                log,
                Callback,
                refused.Details.ContainsKey(RetryAt) ? CallbackCheck.RateLimit : CallbackCheck.Reference,
                request.HttpContext.TraceIdentifier);
        }

        return Answers.Of(outcome, TypedResults.Ok());
    }

    // One value a name: a parameter the gateway repeated is not one it sent.
    private static Dictionary<string, string> Parameters(IQueryCollection query)
    {
        var parameters = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (KeyValuePair<string, StringValues> parameter in query)
        {
            if (parameter.Value.Count == 1)
            {
                parameters[parameter.Key] = parameter.Value.ToString();
            }
        }

        return parameters;
    }
}
