using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Credentials;
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
/// Implements AUTH-SESS-002, REG-MAIL-002, IDN-LIFE-012a, IDN-AUD-001, IDN-PRIN-001 and
/// CONV-DESIGN-003. The
/// credential is named by its identifier and never by anything a person typed, so the
/// row holds no personal attribute; a mail app password is named by the identifier the
/// mail server gave it, and its secret never reaches the row (INT-MAIL-010 AC4). A
/// provider's event is named by its type and never by the provider's subject
/// identifier, which the linked credential stands for.
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

    /// <inheritdoc/>
    public async ValueTask RecordedAsync(
        AuditAction action,
        SystemPrincipal principal,
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
                    principal,
                    subject,
                    organization: null,
                    new Dictionary<string, JsonElement>(capacity: 1, StringComparer.Ordinal)
                    {
                        [Credential] = JsonSerializer.SerializeToElement(credential.ToString()),
                    }),
                cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc/>
    public async ValueTask MailCredentialAsync(
        AuditAction action,
        SubjectId subject,
        string credential,
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
                        [Credential] = JsonSerializer.SerializeToElement(credential),
                    }),
                cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc/>
    public async ValueTask ProviderEventAsync(
        AuditAction action,
        SubjectId subject,
        AuthenticatorId credential,
        string type,
        ProviderEventOutcome outcome,
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
                    new Dictionary<string, JsonElement>(capacity: 3, StringComparer.Ordinal)
                    {
                        [Credential] = JsonSerializer.SerializeToElement(credential.ToString()),
                        ["event"] = JsonSerializer.SerializeToElement(type),
                        ["outcome"] = JsonSerializer.SerializeToElement(
                            VocabularyConverter<ProviderEventOutcome>.Write(outcome)),
                    }),
                cancellationToken)
            .ConfigureAwait(false);
}
