using System;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Credentials;
using Janus.Core;

namespace Janus.Authentication.Factors;

/// <summary>
/// Where an event about one credential is recorded. The area holds no audit trail of
/// its own, so what it has to record it hands out through this.
/// </summary>
/// <remarks>
/// Implements AUTH-FACT-014, REG-MAIL-002, IDN-LIFE-012a, IDN-AUD-001, IDN-PRIN-001 and
/// CONV-LAYOUT-001.
/// </remarks>
internal interface ICredentialAudit
{
    /// <summary>
    /// Records that something happened to a credential.
    /// </summary>
    /// <param name="action">What happened.</param>
    /// <param name="subject">Whose credential.</param>
    /// <param name="credential">Which credential.</param>
    /// <param name="at">When.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of recording it.</returns>
    ValueTask RecordedAsync(
        AuditAction action,
        SubjectId subject,
        AuthenticatorId credential,
        DateTimeOffset at,
        CancellationToken cancellationToken);

    /// <summary>
    /// Records that background work did something to a credential, under the system
    /// principal it ran as (IDN-PRIN-001, INF-BG-002).
    /// </summary>
    /// <param name="action">What happened.</param>
    /// <param name="principal">The principal that did it, with its stated reason.</param>
    /// <param name="subject">Whose credential.</param>
    /// <param name="credential">Which credential.</param>
    /// <param name="at">When.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of recording it.</returns>
    ValueTask RecordedAsync(
        AuditAction action,
        SystemPrincipal principal,
        SubjectId subject,
        AuthenticatorId credential,
        DateTimeOffset at,
        CancellationToken cancellationToken);

    /// <summary>
    /// Records that something happened to a mail app password, which the mail server
    /// holds and names.
    /// </summary>
    /// <param name="action">What happened.</param>
    /// <param name="subject">Whose app password.</param>
    /// <param name="credential">What the server calls it.</param>
    /// <param name="at">When.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of recording it.</returns>
    ValueTask MailCredentialAsync(
        AuditAction action,
        SubjectId subject,
        string credential,
        DateTimeOffset at,
        CancellationToken cancellationToken);

    /// <summary>
    /// Records a social provider's security event about the identity a credential links,
    /// and what it did.
    /// </summary>
    /// <param name="action">Whether the event was taken or refused.</param>
    /// <param name="subject">Whose credential.</param>
    /// <param name="credential">Which credential.</param>
    /// <param name="type">The event's type, as the provider spells it.</param>
    /// <param name="outcome">What it did, or why it did nothing.</param>
    /// <param name="at">When.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of recording it.</returns>
    ValueTask ProviderEventAsync(
        AuditAction action,
        SubjectId subject,
        AuthenticatorId credential,
        string type,
        ProviderEventOutcome outcome,
        DateTimeOffset at,
        CancellationToken cancellationToken);
}
