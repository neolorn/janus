using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Sending;
using Janus.Core;
using Janus.Identity.Audit;

namespace Janus.Storage.Authentication.Sending;

/// <summary>
/// What a restriction edit or a grant leaves in the audit trail.
/// </summary>
/// <param name="records">Where the trail is appended to.</param>
/// <param name="time">The clock the deployment runs on.</param>
/// <remarks>
/// Implements AUTH-ABUSE-004 and IDN-AUD-001. A grant records the restriction, the
/// credit and the reason; the key it was granted to is never written down.
/// </remarks>
internal sealed class SendAudit(IAuditStore records, TimeProvider time) : ISendAudit
{
    private static readonly AuditAction Edited = AuditAction.Parse("auth.restriction.edited");

    private static readonly AuditAction Granted = AuditAction.Parse("auth.restriction.granted");

    /// <inheritdoc/>
    public async ValueTask EditedAsync(
        string restriction,
        bool loosening,
        string? reason,
        SubjectId actor,
        DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        var details = new Dictionary<string, JsonElement>(capacity: 3, StringComparer.Ordinal)
        {
            ["restriction"] = JsonSerializer.SerializeToElement(restriction),
            ["loosening"] = JsonSerializer.SerializeToElement(loosening),
        };

        if (reason is not null)
        {
            details["reason"] = JsonSerializer.SerializeToElement(reason);
        }

        await AppendAsync(Edited, actor, at, details, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask GrantedAsync(
        string restriction,
        int credit,
        string reason,
        SubjectId actor,
        DateTimeOffset at,
        CancellationToken cancellationToken) =>
        await AppendAsync(
                Granted,
                actor,
                at,
                new Dictionary<string, JsonElement>(capacity: 3, StringComparer.Ordinal)
                {
                    ["restriction"] = JsonSerializer.SerializeToElement(restriction),
                    ["credit"] = JsonSerializer.SerializeToElement(credit),
                    ["reason"] = JsonSerializer.SerializeToElement(reason),
                },
                cancellationToken)
            .ConfigureAwait(false);

    private async ValueTask AppendAsync(
        AuditAction action,
        SubjectId actor,
        DateTimeOffset at,
        Dictionary<string, JsonElement> details,
        CancellationToken cancellationToken) =>
        await records
            .AppendAsync(
                AuditRecord.Of(
                    AuditRecordId.New(time),
                    AuditCategory.Security,
                    action,
                    at,
                    actor,
                    actor,
                    organization: null,
                    details),
                cancellationToken)
            .ConfigureAwait(false);
}
