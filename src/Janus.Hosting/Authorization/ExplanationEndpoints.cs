using System;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Hosting.Bff;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Janus.Hosting.Authorization;

/// <summary>
/// The two resolutions of a refusal's correlation identifier of chapter 09 section 8a:
/// the support role's, and the caller's own.
/// </summary>
/// <remarks>
/// Implements AUTHZ-GATE-004, AUTHZ-CONCEAL-004, LIB-API-005 and CONV-DESIGN-006. Each
/// is one line to <see cref="IAccessGate"/>, which judges who may resolve what.
/// </remarks>
internal static class ExplanationEndpoints
{
    /// <summary>
    /// Mounts them.
    /// </summary>
    /// <param name="endpoints">Where the host is mounting the library.</param>
    /// <returns>The builder, so the caller can go on.</returns>
    /// <exception cref="ArgumentNullException">The route builder is absent.</exception>
    public static IEndpointRouteBuilder MapExplanations(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        _ = SessionRequired.On(endpoints.MapGet("/admin/explanations/{correlationId}", ResolveAsync))
            .Declares(EndpointDeclaration
                .Answering(
                    ErrorCodes.Denied)
                .Binding<AuditRecordId>("correlationId"))
            .Produces<ExplanationView>();
        _ = SessionRequired.On(endpoints.MapGet("/account/explanations/{correlationId}", ResolveOwnAsync))
            .Declares(EndpointDeclaration
                .Answering(
                    ErrorCodes.Denied)
                .Binding<AuditRecordId>("correlationId"))
            .Produces<ExplanationView>();

        return endpoints;
    }

    private static async Task<IResult> ResolveAsync(
        IAccessGate gate,
        RequestSession browser,
        AuditRecordId correlationId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(gate);
        ArgumentNullException.ThrowIfNull(browser);

        return Answers.Of(
            await gate
                .ResolveAsync(
                    browser.Asking,
                    correlationId,
                    cancellationToken)
                .ConfigureAwait(false),
            Explained);
    }

    private static async Task<IResult> ResolveOwnAsync(
        IAccessGate gate,
        RequestSession browser,
        AuditRecordId correlationId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(gate);
        ArgumentNullException.ThrowIfNull(browser);

        return Answers.Of(
            await gate
                .ResolveOwnAsync(
                    browser.Asking,
                    correlationId,
                    cancellationToken)
                .ConfigureAwait(false),
            Explained);
    }

    private static IResult Explained(AccessExplanation explanation) => TypedResults.Json(
        ExplanationView.Of(explanation),
        AuthorizationJson.Default.ExplanationView,
        contentType: null,
        StatusCodes.Status200OK);
}
