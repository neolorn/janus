using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Credentials;
using Janus.Authentication.Factors;
using Janus.Core;

namespace Janus.Authentication.Tests.Factors;

/// <summary>
/// The audit trail of what happened to a credential, held so a test can read it.
/// </summary>
internal sealed class CredentialAuditInMemory : ICredentialAudit
{
    /// <summary>
    /// What was recorded, in the order it was recorded.
    /// </summary>
    public List<(AuditAction Action, SubjectId Subject, AuthenticatorId Credential)> Records { get; } = [];

    /// <summary>
    /// The system principal each record was made under, in the order it was recorded;
    /// nothing where a person acted.
    /// </summary>
    public List<SystemPrincipal?> Principals { get; } = [];

    /// <summary>
    /// What was recorded of mail app passwords, in the order it was recorded.
    /// </summary>
    public List<(AuditAction Action, SubjectId Subject, string Credential)> MailCredentials { get; } = [];

    /// <summary>
    /// What was recorded of social providers' security events, in the order it was
    /// recorded.
    /// </summary>
    public List<(AuditAction Action, AuthenticatorId Credential, string Type, ProviderEventOutcome Outcome)> ProviderEvents { get; } = [];

    /// <inheritdoc/>
    public ValueTask RecordedAsync(
        AuditAction action,
        SubjectId subject,
        AuthenticatorId credential,
        DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        Records.Add((action, subject, credential));
        Principals.Add(null);

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask RecordedAsync(
        AuditAction action,
        SystemPrincipal principal,
        SubjectId subject,
        AuthenticatorId credential,
        DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        Records.Add((action, subject, credential));
        Principals.Add(principal);

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask MailCredentialAsync(
        AuditAction action,
        SubjectId subject,
        string credential,
        DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        MailCredentials.Add((action, subject, credential));

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask ProviderEventAsync(
        AuditAction action,
        SubjectId subject,
        AuthenticatorId credential,
        string type,
        ProviderEventOutcome outcome,
        DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        ProviderEvents.Add((action, credential, type, outcome));

        return ValueTask.CompletedTask;
    }
}
