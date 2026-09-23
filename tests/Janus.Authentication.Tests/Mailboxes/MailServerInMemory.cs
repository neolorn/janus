using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Authentication.Tests.Mailboxes;

/// <summary>
/// A mail server that hosts mailboxes in memory and honours the contract: a push whose
/// key it has applied changes nothing, and a listing answers what it holds.
/// </summary>
internal sealed class MailServerInMemory : IMailServer
{
    private readonly Dictionary<string, bool> _hosted = new(StringComparer.Ordinal);

    private readonly HashSet<Guid> _applied = [];

    /// <summary>
    /// Every push received, in order, repeats included.
    /// </summary>
    public List<MailboxPush> Received { get; } = [];

    /// <summary>
    /// Every push that changed something, in order.
    /// </summary>
    public List<MailboxPush> Applied { get; } = [];

    /// <summary>
    /// Whether every call fails, as one made while the server is unreachable does.
    /// </summary>
    public bool Unreachable { get; set; }

    /// <summary>
    /// Whether a push is applied and its answer then lost, as one whose connection
    /// drops after the server acted does.
    /// </summary>
    public bool LosesAnswers { get; set; }

    /// <summary>
    /// What the test observes the moment a push arrives, before the server acts on it.
    /// </summary>
    public Action<MailboxPush>? Receiving { get; set; }

    /// <summary>
    /// Whether the server hosts a mailbox, and whether it is enabled.
    /// </summary>
    /// <param name="address">The address.</param>
    /// <returns>Whether it is enabled, or nothing where it is not hosted.</returns>
    public bool? Hosts(string address) =>
        _hosted.TryGetValue(address, out bool enabled) ? enabled : null;

    /// <summary>
    /// Changes a mailbox on the server behind the library's back, as an operator
    /// working in the server's own console does.
    /// </summary>
    /// <param name="address">The address.</param>
    /// <param name="enabled">Whether it is enabled, or nothing to remove it.</param>
    public void Set(string address, bool? enabled)
    {
        if (enabled is bool value)
        {
            _hosted[address] = value;
        }
        else
        {
            _ = _hosted.Remove(address);
        }
    }

    /// <inheritdoc/>
    public ValueTask<Result> ProvisionAsync(MailboxPush push, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(push);

        Received.Add(push);
        Receiving?.Invoke(push);

        if (Unreachable)
        {
            return ValueTask.FromResult(Result.Failure(Error.From(ErrorCodes.SystemFault)));
        }

        if (_applied.Add(push.Key))
        {
            Applied.Add(push);
            Set(push.Address, push.State switch
            {
                MailboxState.Enabled => true,
                MailboxState.Disabled => false,
                _ => null,
            });
        }

        return ValueTask.FromResult(
            LosesAnswers ? Result.Failure(Error.From(ErrorCodes.SystemFault)) : Result.Success());
    }

    /// <inheritdoc/>
    public ValueTask<Result<IReadOnlyList<HostedMailbox>>> MailboxesAsync(CancellationToken cancellationToken) =>
        ValueTask.FromResult(
            Unreachable
                ? Result.Failure<IReadOnlyList<HostedMailbox>>(Error.From(ErrorCodes.SystemFault))
                : Result.Success<IReadOnlyList<HostedMailbox>>(
                    [.. _hosted.Select(pair => new HostedMailbox(pair.Key, pair.Value))]));
}
