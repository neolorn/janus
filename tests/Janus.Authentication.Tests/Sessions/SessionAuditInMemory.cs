using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Sessions;
using Janus.Core;

namespace Janus.Authentication.Tests.Sessions;

/// <summary>
/// The audit trail, holding what was presented so a test can read the one place a
/// factor is named.
/// </summary>
internal sealed class SessionAuditInMemory : ISessionAudit
{
    /// <summary>
    /// What each authentication presented, in the order it was recorded.
    /// </summary>
    public List<(SessionId Session, SubjectId Subject, IReadOnlyCollection<Factor> Presented)> Records { get; } = [];

    /// <inheritdoc/>
    public ValueTask PresentedAsync(
        SessionId session,
        SubjectId subject,
        IReadOnlyCollection<Factor> presented,
        DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        Records.Add((session, subject, presented));

        return ValueTask.CompletedTask;
    }
}
