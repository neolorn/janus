using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Sending;
using Janus.Core.Configuration;

namespace Janus.Authentication.Tests.Sending;

/// <summary>
/// The audit trail of bot-defence signals, which is the whole of what happens where
/// no verifier is registered.
/// </summary>
internal sealed class BotDefenceAuditInMemory : IBotDefenceAudit
{
    /// <summary>
    /// Every signal recorded, in order.
    /// </summary>
    public List<(BotDefenceSignal Signal, string Source, bool Challenged)> Records { get; } = [];

    /// <inheritdoc/>
    public ValueTask SignalledAsync(
        BotDefenceSignal signal,
        string source,
        bool challenged,
        DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        Records.Add((signal, source, challenged));

        return ValueTask.CompletedTask;
    }
}
