using System;
using System.Collections.Generic;
using Janus.Core;

namespace Janus.Authentication.Tests.Sending;

/// <summary>
/// One channel's view of the host's notification handler: every message taken for it,
/// and whether it takes what it is handed.
/// </summary>
internal sealed class ChannelInMemory
{
    /// <summary>
    /// Every message taken, in order.
    /// </summary>
    public List<SendRequest> Taken { get; } = [];

    /// <summary>
    /// Whether the handler takes what it is handed on this channel.
    /// </summary>
    public bool Accepts { get; set; } = true;

    /// <summary>
    /// How many messages the handler takes on this channel before it refuses the rest.
    /// </summary>
    public int Takes { get; set; } = int.MaxValue;

    /// <summary>
    /// What a test runs as each message is handed over, where it watches for the moment.
    /// </summary>
    public Action? Handed { get; set; }

    /// <summary>
    /// Takes one message, or refuses it as the test says.
    /// </summary>
    /// <param name="request">The message.</param>
    /// <returns>Nothing, or the refusal.</returns>
    public Result Take(SendRequest request)
    {
        Handed?.Invoke();

        if (!Accepts || Taken.Count >= Takes)
        {
            return Result.Failure(Error.From(ErrorCodes.SystemFault));
        }

        Taken.Add(request);

        return Result.Success();
    }
}
