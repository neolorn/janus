using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Sending;
using Janus.Core;

namespace Janus.Authentication.Tests.Sending;

/// <summary>
/// The sending restrictions as an ask that sends nothing draws on them, keeping each
/// send drawn so a test can read what was counted in the place of a message. How the
/// restrictions judge it is tested where the sending path is.
/// </summary>
internal sealed class SendingRestrictionsInMemory : ISendingRestrictions
{
    /// <summary>
    /// The unit of work the operations under test run in. Where a test names it, a draw
    /// made outside it is a fault, as it is in the library.
    /// </summary>
    public UnitOfWorkInMemory? Work { get; set; }

    /// <summary>
    /// Every send drawn, in order.
    /// </summary>
    public List<OutboundMessage> Drawn { get; } = [];

    /// <summary>
    /// What the restrictions answer with instead of counting the send, where a test
    /// stands in for a refusal.
    /// </summary>
    public Error? Refusal { get; set; }

    /// <inheritdoc/>
    public ValueTask<Result> DrawAsync(OutboundMessage message, CancellationToken cancellationToken)
    {
        if (Work is { Open: false })
        {
            throw new InvalidOperationException("A send is drawn inside the caller's unit of work.");
        }

        if (Refusal is Error refused)
        {
            return ValueTask.FromResult(Result.Failure(refused));
        }

        Drawn.Add(message);

        return ValueTask.FromResult(Result.Success());
    }
}
