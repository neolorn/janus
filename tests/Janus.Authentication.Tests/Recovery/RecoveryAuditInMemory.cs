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
        DateTimeOffset At)
    {
        /// <summary>
        /// The reason given at the use of the break-glass credential, where the change
        /// was made in the session it opened, or nothing.
        /// </summary>
        public string? BreakGlassReason { get; init; }
    }

    private readonly List<Entry> _written = [];

    /// <summary>
    /// What has been recorded, oldest first.
    /// </summary>
    public IReadOnlyList<Entry> Written => _written;

    /// <summary>
    /// The unit of work the operations under test run in. Where a test names it, a
    /// record written inside one that rolls back is forgotten, as the trail forgets it.
    /// </summary>
    public UnitOfWorkInMemory? Work { get; set; }

    /// <inheritdoc/>
    public ValueTask ApprovedAsync(
        SubjectId approver,
        string? breakGlassReason,
        SubjectId subject,
        string reason,
        IdentifierKind channel,
        DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        int stood = _written.Count;

        Work?.Undoing(() => _written.RemoveRange(stood, _written.Count - stood));
        _written.Add(new Entry(approver, subject, reason, channel, at) { BreakGlassReason = breakGlassReason });

        return ValueTask.CompletedTask;
    }
}
