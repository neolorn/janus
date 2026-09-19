using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
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

    /// <inheritdoc/>
    public ValueTask RecordedAsync(
        AuditAction action,
        SubjectId subject,
        AuthenticatorId credential,
        DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        Records.Add((action, subject, credential));

        return ValueTask.CompletedTask;
    }
}
