using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Oidc;
using Janus.Core;

namespace Janus.Authentication.Tests.Oidc;

/// <summary>
/// What the provider recorded, held in memory.
/// </summary>
internal sealed class OidcAuditInMemory : IOidcAudit
{
    /// <summary>
    /// The reuses recorded.
    /// </summary>
    public List<(SubjectId Subject, string ClientId, SessionId Session, DateTimeOffset At)> Reuses { get; } = [];

    /// <inheritdoc/>
    public ValueTask ReusedAsync(
        SubjectId subject,
        string clientId,
        SessionId session,
        DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        Reuses.Add((subject, clientId, session, at));

        return ValueTask.CompletedTask;
    }
}
