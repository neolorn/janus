using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Sending;
using Janus.Core;

namespace Janus.Authentication.Tests.Sending;

/// <summary>
/// The audit trail of what was known about a number before a restricted factor went
/// to it, which is the whole of what happens where no provider is registered.
/// </summary>
/// <param name="work">
/// The transaction a record is written in, where a test asks that a record whose unit
/// of work does not commit is forgotten, as the database forgets it (CONV-DESIGN-003).
/// </param>
internal sealed class PhoneSignalAuditInMemory(UnitOfWorkInMemory? work = null) : IPhoneSignalAudit
{
    /// <summary>
    /// Every consideration recorded, in order.
    /// </summary>
    public List<(Factor Factor, PhoneSignal? Signal, SubjectId? Subject)> Records { get; } = [];

    /// <inheritdoc/>
    /// <remarks>
    /// Read from the transaction the fake was given, and none open where it was given
    /// none.
    /// </remarks>
    public bool UnitOfWorkOpen => work?.Open ?? false;

    /// <inheritdoc/>
    public ValueTask ConsideredAsync(
        Factor factor,
        PhoneSignal? signal,
        SubjectId? subject,
        DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        Records.Add((factor, signal, subject));

        // A rollback puts back the latest write first, so the record it forgets is the
        // last one standing.
        work?.Undoing(() => Records.RemoveAt(Records.Count - 1));

        return ValueTask.CompletedTask;
    }
}
