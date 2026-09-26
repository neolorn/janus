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
/// What was presented on a session, and what was refused, written to the audit trail.
/// </summary>
/// <param name="records">Where the trail is appended to.</param>
/// <param name="time">The clock the deployment runs on.</param>
/// <remarks>
/// Implements AUTH-SESS-002, IDN-AUD-001 and CONV-LOG-005. Factor identity is the
/// trail's and the session record holds none of it, so this is the one place a factor
/// is named against an authentication. A refusal names the factor and nothing that was
/// typed: no identifier, no value and no address it came from.
/// </remarks>
internal sealed class SessionAudit(IAuditStore records, TimeProvider time) : ISessionAudit
{
    private static readonly AuditAction Presented = AuditActions.SessionPresented;

    private static readonly AuditAction Failed = AuditActions.AuthenticationFailed;

    private static readonly AuditAction FailedStepUp = AuditActions.StepUpFailed;

    private const string Session = "session";
    private const string Factors = "factors";
    private const string Refused = "factor";

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

    /// <inheritdoc/>
    /// <remarks>
    /// No actor was established, so the acting subject is the nil subject; the
    /// effective subject is the account the attempt was made against, or the nil
    /// subject where there was none, which is the recorded fact.
    /// </remarks>
    public ValueTask FailedAsync(
        SubjectId? subject,
        Factor presented,
        DateTimeOffset at,
        CancellationToken cancellationToken) =>
        records.AppendAsync(
            AuditRecord.Of(
                AuditRecordId.New(time),
                AuditCategory.Security,
                Failed,
                at,
                actingSubject: default,
                subject ?? default,
                organization: null,
                new Dictionary<string, JsonElement>(capacity: 1, StringComparer.Ordinal)
                {
                    [Refused] = JsonSerializer.SerializeToElement(VocabularyConverter<Factor>.Write(presented)),
                }),
            cancellationToken);

    /// <inheritdoc/>
    public ValueTask StepUpFailedAsync(
        SessionId session,
        SubjectId subject,
        Factor presented,
        DateTimeOffset at,
        CancellationToken cancellationToken) =>
        records.AppendAsync(
            AuditRecord.Of(
                AuditRecordId.New(time),
                AuditCategory.Security,
                FailedStepUp,
                at,
                subject,
                subject,
                organization: null,
                new Dictionary<string, JsonElement>(capacity: 2, StringComparer.Ordinal)
                {
                    [Session] = JsonSerializer.SerializeToElement(session.ToString()),
                    [Refused] = JsonSerializer.SerializeToElement(VocabularyConverter<Factor>.Write(presented)),
                }),
            cancellationToken);
}
