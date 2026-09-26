using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication;
using Janus.Authentication.Oidc;
using Janus.Core;
using Janus.Identity.Audit;

namespace Janus.Storage.Authentication.Oidc;

/// <summary>
/// What the provider did to a session, written to the audit trail.
/// </summary>
/// <param name="records">Where the trail is appended to.</param>
/// <param name="time">The clock the deployment runs on.</param>
/// <remarks>
/// Implements AUTH-OIDC-001, AUTH-OIDC-003, IDN-AUD-001 and CONV-DESIGN-003. A
/// registration made from the server is recorded under the command's principal, naming
/// the client and no account. A reuse is recorded
/// against the account whose session it was, with the client that presented the token
/// and the session everything derived from, because that is what an investigation
/// afterwards needs and nothing narrower would identify what was revoked.
/// </remarks>
internal sealed class OidcAudit(IAuditStore records, TimeProvider time) : IOidcAudit
{
    private static readonly AuditAction Reused = AuditActions.RefreshTokenReused;

    private const string Client = "client";

    private const string Session = "session";

    private const string Kind = "kind";

    private const string Changed = "changed";

    /// <inheritdoc/>
    public async ValueTask ReusedAsync(
        SubjectId subject,
        string clientId,
        SessionId session,
        DateTimeOffset at,
        CancellationToken cancellationToken) =>
        await records.AppendAsync(
                AuditRecord.Of(
                    AuditRecordId.New(time),
                    AuditCategory.Security,
                    Reused,
                    at,
                    subject,
                    subject,
                    organization: null,
                    new Dictionary<string, JsonElement>(capacity: 2, StringComparer.Ordinal)
                    {
                        [Client] = JsonSerializer.SerializeToElement(clientId),
                        [Session] = JsonSerializer.SerializeToElement(session.ToString()),
                    }),
                cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc/>
    public async ValueTask RegisteredAsync(
        SystemPrincipal principal,
        string clientId,
        OidcClientKind kind,
        bool changed,
        DateTimeOffset at,
        CancellationToken cancellationToken) =>
        await records.AppendAsync(
                AuditRecord.Of(
                    AuditRecordId.New(time),
                    AuditCategory.Security,
                    AuditActions.ClientRegistered,
                    at,
                    principal,
                    effectiveSubject: null,
                    organization: null,
                    new Dictionary<string, JsonElement>(capacity: 3, StringComparer.Ordinal)
                    {
                        [Client] = JsonSerializer.SerializeToElement(clientId),
                        [Kind] = JsonSerializer.SerializeToElement(WrittenName.Of(kind)),
                        [Changed] = JsonSerializer.SerializeToElement(changed),
                    }),
                cancellationToken)
            .ConfigureAwait(false);
}
