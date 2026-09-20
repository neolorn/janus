using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Recovery;
using Janus.Core;

namespace Janus.Authentication.Tests.Recovery;

/// <summary>
/// What an approval wrote to the audit trail, kept where a test can read it.
/// </summary>
internal sealed class RecoveryAuditInMemory : IRecoveryAudit
{
    /// <summary>
    /// One recorded approval.
    /// </summary>
    /// <param name="Approver">Who approved.</param>
    /// <param name="Subject">Whose account.</param>
    /// <param name="Reason">The written reason.</param>
    /// <param name="Channel">Which kind of channel carried the confirmation.</param>
    /// <param name="At">When.</param>
    internal sealed record Entry(
        SubjectId Approver,
        SubjectId Subject,
        string Reason,
        IdentifierKind Channel,
        DateTimeOffset At);

    private readonly List<Entry> _written = [];

    /// <summary>
    /// What has been recorded, oldest first.
    /// </summary>
    public IReadOnlyList<Entry> Written => _written;

    /// <inheritdoc/>
    public ValueTask ApprovedAsync(
        SubjectId approver,
        SubjectId subject,
        string reason,
        IdentifierKind channel,
        DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        _written.Add(new Entry(approver, subject, reason, channel, at));

        return ValueTask.CompletedTask;
    }
}
