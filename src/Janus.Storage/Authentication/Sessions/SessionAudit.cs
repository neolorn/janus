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
    private const string Verification = "verification";

    // The one verification whose refused code is an authentication failure: the
    // new-device check's (CONV-LOG-005).
    private const string Device = "device";

    /// <inheritdoc/>
    public async ValueTask PresentedAsync(
        SessionId session,
        SubjectId subject,
        string? breakGlassReason,
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
                    breakGlassReason,
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
    /// No actor was established, so the acting and effective identities are the nil
    /// subject; the record's subject is the account the attempt was made against, or
    /// nothing where there was none, which is the recorded fact (IDN-AUD-001, D-166).
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
                subject,
                breakGlassReason: null,
                organization: null,
                new Dictionary<string, JsonElement>(capacity: 1, StringComparer.Ordinal)
                {
                    [Refused] = JsonSerializer.SerializeToElement(VocabularyConverter<Factor>.Write(presented)),
                }),
            cancellationToken);

    /// <inheritdoc/>
    /// <remarks>
    /// No actor was established, so the acting subject is the nil subject; the details
    /// name the verification and no factor, since the code refused was none.
    /// </remarks>
    public ValueTask DeviceVerificationFailedAsync(
        SubjectId? subject,
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
                breakGlassReason: null,
                organization: null,
                new Dictionary<string, JsonElement>(capacity: 1, StringComparer.Ordinal)
                {
                    [Verification] = JsonSerializer.SerializeToElement(Device),
                }),
            cancellationToken);

    /// <inheritdoc/>
    public ValueTask StepUpFailedAsync(
        SessionId session,
        SubjectId subject,
        string? breakGlassReason,
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
                breakGlassReason,
                organization: null,
                new Dictionary<string, JsonElement>(capacity: 2, StringComparer.Ordinal)
                {
                    [Session] = JsonSerializer.SerializeToElement(session.ToString()),
                    [Refused] = JsonSerializer.SerializeToElement(VocabularyConverter<Factor>.Write(presented)),
                }),
            cancellationToken);
}
