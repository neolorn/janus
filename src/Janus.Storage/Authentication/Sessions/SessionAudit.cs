using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Sessions;
using Janus.Core;
using Janus.Identity.Audit;

namespace Janus.Storage.Authentication.Sessions;

/// <summary>
/// What was presented on a session, written to the audit trail.
/// </summary>
/// <param name="records">Where the trail is appended to.</param>
/// <param name="time">The clock the deployment runs on.</param>
/// <remarks>
/// Implements AUTH-SESS-002 and IDN-AUD-001. Factor identity is the trail's and the
/// session record holds none of it, so this is the one place a factor is named
/// against an authentication.
/// </remarks>
internal sealed class SessionAudit(IAuditStore records, TimeProvider time) : ISessionAudit
{
    private static readonly AuditAction Presented = AuditActions.SessionPresented;

    private const string Session = "session";
    private const string Factors = "factors";

    /// <inheritdoc/>
    public async ValueTask PresentedAsync(
        SessionId session,
        SubjectId subject,
        IReadOnlyCollection<Factor> presented,
        DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(presented);

        await records.AppendAsync(
                AuditRecord.Of(
                    AuditRecordId.New(time),
                    AuditCategory.Security,
                    Presented,
                    at,
                    subject,
                    subject,
                    organization: null,
                    new Dictionary<string, JsonElement>(capacity: 2, StringComparer.Ordinal)
                    {
                        [Session] = JsonSerializer.SerializeToElement(session.ToString()),
                        [Factors] = JsonSerializer.SerializeToElement(
                            presented.Select(factor => VocabularyConverter<Factor>.Write(factor)).ToArray()),
                    }),
                cancellationToken)
            .ConfigureAwait(false);
    }
}
