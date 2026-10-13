using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.BreakGlass;
using Janus.Core;

namespace Janus.Authentication.Tests.BreakGlass;

/// <summary>
/// Where what happened to the break-glass credential is written down, held in order.
/// </summary>
internal sealed class BreakGlassAuditInMemory : IBreakGlassAudit
{
    /// <summary>
    /// Every generation: who, which issue, which it replaced, when, and the reason of the
    /// break-glass session it was made in, where it was.
    /// </summary>
    public List<(SubjectId Acting, BreakGlassCredentialId Credential, BreakGlassCredentialId? Replaced, DateTimeOffset At, string? BreakGlassReason)> Generated { get; } = [];

    /// <summary>
    /// Every use: the emergency account, which issue, the session it opened, when, and
    /// the reason given with it.
    /// </summary>
    public List<(SubjectId Emergency, BreakGlassCredentialId Credential, SessionId Session, DateTimeOffset At, string? BreakGlassReason)> Used { get; } = [];

    /// <inheritdoc/>
    public ValueTask GeneratedAsync(
        SubjectId acting,
        string? breakGlassReason,
        BreakGlassCredentialId credential,
        BreakGlassCredentialId? replaced,
        DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        Generated.Add((acting, credential, replaced, at, breakGlassReason));

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask UsedAsync(
        SubjectId emergency,
        string? breakGlassReason,
        BreakGlassCredentialId credential,
        SessionId session,
        DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        Used.Add((emergency, credential, session, at, breakGlassReason));

        return ValueTask.CompletedTask;
    }
}
