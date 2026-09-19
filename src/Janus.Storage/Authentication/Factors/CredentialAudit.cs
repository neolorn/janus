using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Factors;
using Janus.Core;
using Janus.Identity.Audit;

namespace Janus.Storage.Authentication.Factors;

/// <summary>
/// What happens to a credential, written to the audit trail.
/// </summary>
/// <param name="records">Where the trail is appended to.</param>
/// <param name="time">The clock the deployment runs on.</param>
/// <remarks>
/// Implements AUTH-SESS-002, IDN-AUD-001 and CONV-DESIGN-003. The credential is named
/// by its identifier and never by anything a person typed, so the row holds no
/// personal attribute.
/// </remarks>
internal sealed class CredentialAudit(IAuditStore records, TimeProvider time) : ICredentialAudit
{
    private const string Credential = "credential";

    /// <inheritdoc/>
    public async ValueTask RecordedAsync(
        AuditAction action,
        SubjectId subject,
        AuthenticatorId credential,
        DateTimeOffset at,
        CancellationToken cancellationToken) =>
        await records.AppendAsync(
                AuditRecord.Of(
                    AuditRecordId.New(time),
                    AuditCategory.Security,
                    action,
                    at,
                    subject,
                    subject,
                    organization: null,
                    new Dictionary<string, JsonElement>(capacity: 1, StringComparer.Ordinal)
                    {
                        [Credential] = JsonSerializer.SerializeToElement(credential.ToString()),
                    }),
                cancellationToken)
            .ConfigureAwait(false);
}
