using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Sessions;
using Janus.Core;

namespace Janus.Authentication.Tests.Sessions;

/// <summary>
/// The audit trail, holding what was presented and what was refused so a test can read
/// the one place a factor is named.
/// </summary>
internal sealed class SessionAuditInMemory : ISessionAudit
{
    /// <summary>
    /// What each authentication presented, in the order it was recorded.
    /// </summary>
    public List<(SessionId Session, SubjectId Subject, IReadOnlyCollection<Factor> Presented)> Records { get; } = [];

    /// <summary>
    /// Each factor refused at authentication, with the account it was presented
    /// against where there was one, in the order it was recorded.
    /// </summary>
    public List<(SubjectId? Subject, Factor Presented)> Failed { get; } = [];

    /// <summary>
    /// Each factor refused at a step-up, in the order it was recorded.
    /// </summary>
    public List<(SessionId Session, SubjectId Subject, Factor Presented)> StepUpsFailed { get; } = [];

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

    /// <inheritdoc/>
    public ValueTask FailedAsync(
        SubjectId? subject,
        Factor presented,
        DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        Failed.Add((subject, presented));

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask StepUpFailedAsync(
        SessionId session,
        SubjectId subject,
        Factor presented,
        DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        StepUpsFailed.Add((session, subject, presented));

        return ValueTask.CompletedTask;
    }
}
