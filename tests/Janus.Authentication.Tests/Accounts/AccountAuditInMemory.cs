using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Accounts;
using Janus.Core;

namespace Janus.Authentication.Tests.Accounts;

/// <summary>
/// The trail of what an account changed about itself, held in memory.
/// </summary>
internal sealed class AccountAuditInMemory : IAccountAudit
{
    private readonly List<RecordedChange> _recorded = [];

    /// <summary>
    /// What was recorded, in the order it was.
    /// </summary>
    public IReadOnlyList<RecordedChange> Recorded => _recorded;

    /// <inheritdoc/>
    public ValueTask RecordedAsync(
        AuditAction action,
        SubjectId acting,
        SubjectId subject,
        DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        _recorded.Add(new RecordedChange(action, acting, subject, at));

        return ValueTask.CompletedTask;
    }
}
