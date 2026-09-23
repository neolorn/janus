using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Hosting.Bff;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Janus.Hosting.Privacy;

/// <summary>
/// The audit trail read by subject, of chapter 09 section 8a.
/// </summary>
/// <remarks>
/// Implements PRIV-BREACH-002, LIB-API-005 and CONV-DESIGN-006. The endpoint is one
/// line to <see cref="IAuditTrail"/>, which judges the permission.
/// </remarks>
internal static class AuditTrailEndpoints
{
    /// <summary>
    /// Mounts it.
    /// </summary>
    /// <param name="endpoints">Where the host is mounting the library.</param>
    /// <returns>The builder, so the caller can go on.</returns>
    /// <exception cref="ArgumentNullException">The route builder is absent.</exception>
    public static IEndpointRouteBuilder MapAuditTrail(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        _ = SessionRequired.On(endpoints.MapGet("/admin/audit", OfSubjectAsync));

        return endpoints;
    }

    private static async Task<IResult> OfSubjectAsync(
        IAuditTrail trail,
        RequestSession browser,
        string? subject,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(trail);
        ArgumentNullException.ThrowIfNull(browser);

        if (!Guid.TryParse(subject, out Guid whose))
        {
            return Answers.Malformed("subject");
        }

        return Answers.Of(
            await trail
                .OfSubjectAsync(
                    AccessContext.Of(browser.Required.Subject),
                    new SubjectId(whose),
                    cancellationToken)
                .ConfigureAwait(false),
            entries => TypedResults.Json(
                (IReadOnlyList<AuditEntryView>)[.. entries.Select(AuditEntryView.Of)],
                PrivacyJson.Default.IReadOnlyListAuditEntryView,
                contentType: null,
                StatusCodes.Status200OK));
    }
}
