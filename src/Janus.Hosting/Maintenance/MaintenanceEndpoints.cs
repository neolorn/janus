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

namespace Janus.Hosting.Maintenance;

/// <summary>
/// The licence and maintenance log endpoints of the compliance records, chapter 09
/// section 8a.
/// </summary>
/// <remarks>
/// Implements OPS-MAINT-001. Every one answers to <c>compliance:manage</c>. The log
/// has no endpoint that changes or removes an entry.
/// </remarks>
internal static class MaintenanceEndpoints
{
    private static readonly IResult Nothing = TypedResults.NoContent();

    /// <summary>
    /// Mounts them.
    /// </summary>
    /// <param name="endpoints">Where the host is mounting the library.</param>
    /// <returns>The builder, so the caller can go on.</returns>
    /// <exception cref="ArgumentNullException">The route builder is absent.</exception>
    public static IEndpointRouteBuilder MapMaintenance(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        _ = SessionRequired.On(endpoints.MapGet("/admin/compliance/licences", LicencesAsync));
        _ = SessionRequired.On(endpoints.MapPut("/admin/compliance/licences", ReplaceAsync));
        _ = SessionRequired.On(endpoints.MapGet("/admin/compliance/maintenance", LogAsync));
        _ = SessionRequired.On(endpoints.MapPost("/admin/compliance/maintenance", RecordAsync));

        return endpoints;
    }

    private static async Task<IResult> LicencesAsync(
        IMaintenanceRecords records,
        RequestSession browser,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(records);
        ArgumentNullException.ThrowIfNull(browser);

        return Answers.Of(
            await records
                .LicencesAsync(AccessContext.Of(browser.Required.Subject), cancellationToken)
                .ConfigureAwait(false),
            licences => TypedResults.Json(
                new LicencesView([.. licences.Select(LicenceView.Of)]),
                MaintenanceJson.Default.LicencesView,
                contentType: null,
                StatusCodes.Status200OK));
    }

    private static async Task<IResult> ReplaceAsync(
        LicencesBody body,
        IMaintenanceRecords records,
        RequestSession browser,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(body);
        ArgumentNullException.ThrowIfNull(records);
        ArgumentNullException.ThrowIfNull(browser);

        (IReadOnlyList<Licence>? licences, string member) = body.Read();

        if (licences is null)
        {
            return Answers.Malformed(member);
        }

        return Answers.Of(
            await records
                .ReplaceLicencesAsync(AccessContext.Of(browser.Required.Subject), licences, cancellationToken)
                .ConfigureAwait(false),
            Nothing);
    }

    private static async Task<IResult> LogAsync(
        IMaintenanceRecords records,
        RequestSession browser,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(records);
        ArgumentNullException.ThrowIfNull(browser);

        return Answers.Of(
            await records
                .LogAsync(AccessContext.Of(browser.Required.Subject), cancellationToken)
                .ConfigureAwait(false),
            entries => TypedResults.Json(
                new MaintenanceLogView([.. entries.Select(MaintenanceEntryView.Of)]),
                MaintenanceJson.Default.MaintenanceLogView,
                contentType: null,
                StatusCodes.Status200OK));
    }

    private static async Task<IResult> RecordAsync(
        MaintenanceEntryBody body,
        IMaintenanceRecords records,
        RequestSession browser,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(body);
        ArgumentNullException.ThrowIfNull(records);
        ArgumentNullException.ThrowIfNull(browser);

        if (Performed(body.Task) is not MaintenanceTask task)
        {
            return Answers.Malformed("task");
        }

        if (body.PerformedAt is not { } performedAt)
        {
            return Answers.Malformed("performedAt");
        }

        return Answers.Of(
            await records
                .RecordAsync(
                    AccessContext.Of(browser.Required.Subject),
                    task,
                    performedAt,
                    body.Note,
                    cancellationToken)
                .ConfigureAwait(false),
            entry => TypedResults.Json(
                MaintenanceEntryView.Of(entry),
                MaintenanceJson.Default.MaintenanceEntryView,
                contentType: null,
                StatusCodes.Status201Created));
    }

    private static MaintenanceTask? Performed(string? task) => task switch
    {
        "envelope-rotation" => MaintenanceTask.EnvelopeRotation,
        "licence-renewal" => MaintenanceTask.LicenceRenewal,
        "approver-review" => MaintenanceTask.ApproverReview,
        "pipeline-consumption-review" => MaintenanceTask.PipelineConsumptionReview,
        "risk-trigger-review" => MaintenanceTask.RiskTriggerReview,
        _ => null,
    };
}
