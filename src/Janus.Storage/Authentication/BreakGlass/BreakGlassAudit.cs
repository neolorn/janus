using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.BreakGlass;
using Janus.Core;
using Janus.Identity.Audit;

namespace Janus.Storage.Authentication.BreakGlass;

/// <summary>
/// What happened to the break-glass credential, written to the audit trail.
/// </summary>
/// <param name="records">Where the trail is appended to.</param>
/// <param name="time">The clock the deployment runs on.</param>
/// <remarks>
/// Implements OPS-BOOT-002, OPS-BOOT-004, IDN-AUD-001 and CONV-DESIGN-003. The entry
/// names the issue by its identifier, so the trail shows which one was generated and
/// which used without holding anything of the code.
/// </remarks>
internal sealed class BreakGlassAudit(IAuditStore records, TimeProvider time) : IBreakGlassAudit
{
    /// <inheritdoc/>
    public async ValueTask GeneratedAsync(
        SubjectId acting,
        BreakGlassCredentialId credential,
        BreakGlassCredentialId? replaced,
        DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        var details = new Dictionary<string, JsonElement>(StringComparer.Ordinal)
        {
            ["credential"] = JsonSerializer.SerializeToElement(credential.ToString()),
        };

        if (replaced is BreakGlassCredentialId previous)
        {
            details["replaced"] = JsonSerializer.SerializeToElement(previous.ToString());
        }

        await records.AppendAsync(
                AuditRecord.Of(
                    AuditRecordId.New(time),
                    AuditCategory.Security,
                    AuditActions.BreakGlassGenerated,
                    at,
                    acting,
                    acting,
                    organization: null,
                    details),
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask UsedAsync(
        SubjectId emergency,
        BreakGlassCredentialId credential,
        SessionId session,
        DateTimeOffset at,
        CancellationToken cancellationToken) =>
        await records.AppendAsync(
                AuditRecord.Of(
                    AuditRecordId.New(time),
                    AuditCategory.Security,
                    AuditActions.BreakGlassUsed,
                    at,
                    emergency,
                    emergency,
                    organization: null,
                    new Dictionary<string, JsonElement>(StringComparer.Ordinal)
                    {
                        ["credential"] = JsonSerializer.SerializeToElement(credential.ToString()),
                        ["session"] = JsonSerializer.SerializeToElement(session.ToString()),
                    }),
                cancellationToken)
            .ConfigureAwait(false);
}
