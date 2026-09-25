using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Sending;
using Janus.Core;
using Janus.Hosting.Bff;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Primitives;

namespace Janus.Hosting.Sending;

/// <summary>
/// The SMS gateway's delivery report, chapter 09 section 10, on the machine profile.
/// </summary>
/// <remarks>
/// Implements INT-SMS-005, INT-GEN-003, BFF-MACH-001 and CONV-DESIGN-006. The transport
/// the deployment registers reads the gateway's own parameters; what it read goes to
/// <see cref="DeliveryReports"/>, which can release a send and do nothing else.
/// </remarks>
internal static class DeliveryReportEndpoints
{
    /// <summary>
    /// Mounts it.
    /// </summary>
    /// <param name="endpoints">Where the host is mounting the library.</param>
    /// <returns>The builder, so the caller can go on.</returns>
    /// <exception cref="ArgumentNullException">The route builder is absent.</exception>
    public static IEndpointRouteBuilder MapDeliveryReports(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        _ = endpoints.MapGet("/callbacks/sms/dlr", ReportAsync);

        return endpoints;
    }

    private static async Task<IResult> ReportAsync(
        HttpRequest request,
        ISmsTransport transport,
        DeliveryReports reports,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(transport);
        ArgumentNullException.ThrowIfNull(reports);

        // A report the transport cannot read carries no reference, and is rejected
        // and counted as a forged one is.
        SmsDeliveryReport? report = transport
            .ReadReport(Parameters(request.Query))
            .Match<SmsDeliveryReport?>(value => value, _ => null);

        return Answers.Of(
            await reports
                .ReportAsync(
                    RequestOrigin.Source(request),
                    report?.Reference,
                    report?.Delivered is true,
                    cancellationToken)
                .ConfigureAwait(false),
            TypedResults.Ok());
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
