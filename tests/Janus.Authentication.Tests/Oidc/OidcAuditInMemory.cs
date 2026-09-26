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

    /// <summary>
    /// The registrations recorded.
    /// </summary>
    public List<(SystemPrincipal Principal, string ClientId, OidcClientKind Kind, bool Changed, DateTimeOffset At)> Registrations { get; } = [];

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

    /// <inheritdoc/>
    public ValueTask RegisteredAsync(
        SystemPrincipal principal,
        string clientId,
        OidcClientKind kind,
        bool changed,
        DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        Registrations.Add((principal, clientId, kind, changed, at));

        return ValueTask.CompletedTask;
    }
}
