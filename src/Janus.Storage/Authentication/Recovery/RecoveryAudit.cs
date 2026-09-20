using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Recovery;
using Janus.Core;
using Janus.Identity.Audit;

namespace Janus.Storage.Authentication.Recovery;

/// <summary>
/// What an approver approved, written to the audit trail.
/// </summary>
/// <param name="records">Where the trail is appended to.</param>
/// <param name="time">The clock the deployment runs on.</param>
/// <remarks>
/// Implements AUTH-RECOV-002, AUTH-RECOV-003, IDN-AUD-001 and CONV-DESIGN-003. The
/// acting identity is the approver and the effective identity the account recovered,
/// which is what makes the trail answer who approved for whom. The reason is written
/// about the person, so it is held under their own key and never in the structured
/// fields.
/// </remarks>
internal sealed class RecoveryAudit(IAuditStore records, TimeProvider time) : IRecoveryAudit
{
    private static readonly AuditAction Approved = AuditAction.Parse("auth.recovery.approved");

    private const string Channel = "channel";

    private const string Reason = "reason";

    /// <inheritdoc/>
    public async ValueTask ApprovedAsync(
        SubjectId approver,
        SubjectId subject,
        string reason,
        IdentifierKind channel,
        DateTimeOffset at,
        CancellationToken cancellationToken) =>
        await records.AppendAsync(
                AuditRecord.Of(
                    AuditRecordId.New(time),
                    AuditCategory.Security,
                    Approved,
                    at,
                    approver,
                    subject,
                    organization: null,
                    new Dictionary<string, JsonElement>(capacity: 1, StringComparer.Ordinal)
                    {
                        [Channel] = JsonSerializer.SerializeToElement(
                            VocabularyConverter<IdentifierKind>.Write(channel)),
                    },
                    new Dictionary<string, JsonElement>(capacity: 1, StringComparer.Ordinal)
                    {
                        [Reason] = JsonSerializer.SerializeToElement(reason),
                    }),
                cancellationToken)
            .ConfigureAwait(false);
}
