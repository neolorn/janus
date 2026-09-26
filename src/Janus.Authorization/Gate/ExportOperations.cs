using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authorization.Model;
using Janus.Core;
using Janus.Core.Configuration;

namespace Janus.Authorization.Gate;

/// <summary>
/// What an export operation asks beyond what any other action asks: step-up, a place
/// under the hourly limit, and a row of its own in the audit trail.
/// </summary>
/// <param name="model">The host's declaration, which enumerates the export operations.</param>
/// <param name="ledger">When each actor's recent exports were admitted.</param>
/// <param name="audit">Where each admitted export is recorded.</param>
/// <param name="configuration">Where the step-up flag, the limit and the auditing flag are read.</param>
/// <param name="time">The clock the window is read against.</param>
/// <remarks>
/// Implements OPS-ALERT-006 and D-045. An export is a permission the host declares
/// whose action is <c>export</c> (AC1), and every call through the gate that exercises
/// one and is admitted is one export: a check naming a record, and a list filter or SQL
/// fragment handed out for a query. Whoever acts, a person or a system principal, is
/// gated the same way, so a principal that cannot step up does not export while
/// <c>exfiltration.export.stepuprequired</c> is on.
/// </remarks>
internal sealed class ExportOperations(
    AuthorizationModel model,
    IBulkExportLedger ledger,
    IAccessAudit audit,
    IConfigurationStore configuration,
    TimeProvider time)
{
    // D-045: the limit counts the exports of the last hour, rolling, so it cannot be
    // spent twice across a boundary a clock reset would put in the middle of one sitting.
    private static readonly TimeSpan Window = TimeSpan.FromHours(1);

    /// <summary>
    /// The step-up gate an export asks for while <c>exfiltration.export.stepuprequired</c>
    /// is on, which is named by the export itself.
    /// </summary>
    /// <param name="permission">The permission being exercised or offered.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The gate's name, or nothing where the permission is no export or the flag is off.</returns>
    public async ValueTask<string?> GateOfAsync(Permission permission, CancellationToken cancellationToken)
    {
        if (!model.IsExport(permission))
        {
            return null;
        }

        // An unreadable flag does not lift the gate: its default, on, stands.
        bool required = (await configuration
                .ReadAsync(Settings.ExfiltrationExportStepUpRequired, cancellationToken)
                .ConfigureAwait(false))
            .Match(value => value, _ => Settings.ExfiltrationExportStepUpRequired.Default);

        // D-160: a gate no policy states values for costs what the dearest gate of the
        // person's policy costs, so an export is never cheaper than any named action.
        return required ? permission.ToString() : null;
    }

    /// <summary>
    /// Admits one export the grants and the gates have already allowed: refuses it past
    /// the hourly limit, and otherwise counts it and records it.
    /// </summary>
    /// <param name="context">Who is exporting.</param>
    /// <param name="permission">The permission being exercised.</param>
    /// <param name="type">The kind of record.</param>
    /// <param name="organization">The organization the call is within, where it named one.</param>
    /// <param name="record">The one record, where the call named one.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// Nothing where the permission is no export or the export is admitted, and
    /// <c>auth.throttled</c> carrying <c>retryAt</c> where the actor has spent the hour's
    /// exports.
    /// </returns>
    public async ValueTask<Result> AdmitAsync(
        AccessContext context,
        Permission permission,
        ResourceType type,
        OrganizationId? organization,
        ResourceId? record,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (!model.IsExport(permission))
        {
            return Result.Success();
        }

        DateTimeOffset now = time.GetUtcNow();
        string? principal = context.Principal?.Name;

        int limit = (await configuration
                .ReadAsync(Settings.ExfiltrationExportRateLimit, cancellationToken)
                .ConfigureAwait(false))
            .Match(value => value, _ => Settings.ExfiltrationExportRateLimit.Default);

        IReadOnlyList<DateTimeOffset> taken = await ledger
            .SinceAsync(context.Acting, principal, now - Window, cancellationToken)
            .ConfigureAwait(false);

        // A deployment that set the limit to nothing has closed the door, and the answer
        // is still a refusal with a time on it.
        if (limit <= 0 || taken.Count >= limit)
        {
            DateTimeOffset retryAt = limit <= 0 ? now + Window : taken[^limit] + Window;

            return Result.Failure(Error.From(
                ErrorCodes.Throttled,
                "retryAt",
                JsonSerializer.SerializeToElement(retryAt)));
        }

        await ledger
            .RecordAsync(context.Acting, principal, now, now - Window, cancellationToken)
            .ConfigureAwait(false);

        // OPS-CFG-004: the flag is protected, so what turns the record off is a redeploy
        // and never the person about to export. An unreadable flag records.
        bool auditing = (await configuration
                .ReadAsync(Settings.ExfiltrationExportAuditing, cancellationToken)
                .ConfigureAwait(false))
            .Match(value => value, _ => Settings.ExfiltrationExportAuditing.Default);

        if (auditing)
        {
            await audit
                .RecordAsync(
                    new ExportedAccess(
                        AuditRecordId.New(time),
                        context.Acting,
                        context.Effective,
                        context.Principal,
                        organization,
                        permission,
                        type,
                        record,
                        now),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        return Result.Success();
    }
}
