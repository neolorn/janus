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

    private readonly List<RecordedChange> _administered = [];

    private readonly List<PrivacyRequestId?> _against = [];

    /// <summary>
    /// What was recorded, in the order it was.
    /// </summary>
    public IReadOnlyList<RecordedChange> Recorded => _recorded;

    /// <summary>
    /// What administrators were recorded changing, in the order they did.
    /// </summary>
    public IReadOnlyList<RecordedChange> Administered => _administered;

    /// <summary>
    /// The erasure request each cancellation on a subject's behalf was recorded against,
    /// in the order they were.
    /// </summary>
    public IReadOnlyList<PrivacyRequestId?> Against => _against;

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

    /// <inheritdoc/>
    public ValueTask AdministeredAsync(
        AuditAction action,
        SubjectId acting,
        SubjectId subject,
        DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        _administered.Add(new RecordedChange(action, acting, subject, at));

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask CancelledOnBehalfAsync(
        SubjectId acting,
        SubjectId subject,
        PrivacyRequestId? request,
        DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        _administered.Add(new RecordedChange(AuditActions.DeletionCancelled, acting, subject, at));
        _against.Add(request);

        return ValueTask.CompletedTask;
    }
}
