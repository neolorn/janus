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
internal sealed class PhoneSignalAuditInMemory : IPhoneSignalAudit
{
    /// <summary>
    /// Every consideration recorded, in order.
    /// </summary>
    public List<(Factor Factor, PhoneSignal? Signal, SubjectId? Subject)> Records { get; } = [];

    /// <inheritdoc/>
    public ValueTask ConsideredAsync(
        Factor factor,
        PhoneSignal? signal,
        SubjectId? subject,
        DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        Records.Add((factor, signal, subject));

        return ValueTask.CompletedTask;
    }
}
