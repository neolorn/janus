using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Recovery;
using Janus.Core;

namespace Janus.Authentication.Tests.Recovery;

/// <summary>
/// The approvals standing behind an account's re-enrolment, held in memory as the
/// table holds them.
/// </summary>
internal sealed class RecoveryApprovalStoreInMemory : IRecoveryApprovalStore
{
    private readonly List<RecoveryApproval> _given = [];

    /// <summary>
    /// Every approval given, in order.
    /// </summary>
    public IReadOnlyList<RecoveryApproval> All => _given;

    /// <inheritdoc/>
    public ValueTask AddAsync(RecoveryApproval approval, CancellationToken cancellationToken)
    {
        _given.Add(approval);

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask<IReadOnlyList<RecoveryApproval>> StandingForAsync(
        SubjectId subject,
        DateTimeOffset from,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult<IReadOnlyList<RecoveryApproval>>(
            [.. _given.Where(approval =>
                approval.Subject == subject && approval.At >= from && approval.SpentAt is null)]);

    /// <inheritdoc/>
    public ValueTask<IReadOnlyList<DateTimeOffset>> ForAsync(
        SubjectId subject,
        DateTimeOffset from,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult<IReadOnlyList<DateTimeOffset>>(
            [.. _given
                .Where(approval => approval.Subject == subject && approval.At > from)
                .Select(approval => approval.At)
                .Order()]);

    /// <inheritdoc/>
    public ValueTask<IReadOnlyList<DateTimeOffset>> ByAsync(
        SubjectId approver,
        DateTimeOffset from,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult<IReadOnlyList<DateTimeOffset>>(
            [.. _given
                .Where(approval => approval.Approver == approver && approval.At > from)
                .Select(approval => approval.At)
                .Order()]);

    /// <inheritdoc/>
    public ValueTask SpendAsync(
        SubjectId subject,
        DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        for (int index = 0; index < _given.Count; index++)
        {
            if (_given[index].Subject == subject && _given[index].SpentAt is null)
            {
                _given[index] = _given[index] with { SpentAt = at };
            }
        }

        return ValueTask.CompletedTask;
    }
}
