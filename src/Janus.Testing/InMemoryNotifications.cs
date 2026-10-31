using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Testing;

/// <summary>
/// A notification handler that keeps every message handed to it, readable by
/// destination with its kind's values, and fails a hand-over only where the test says
/// so.
/// </summary>
/// <remarks>
/// Implements LIB-TEST-003 and INT-SMS-006 (D-225). A test reads a code or a link as the
/// value the message carries, never out of a text. A message it fails is not kept, and
/// the library hands it over again under <c>outbox.retry.*</c>.
/// </remarks>
public sealed class InMemoryNotifications : INotificationHandler
{
    private readonly ConcurrentQueue<SendRequest> _taken = new();

    private readonly ConcurrentDictionary<(SendKind Channel, string Destination), bool> _failing = new();

    /// <summary>
    /// Every message taken, in the order it was taken.
    /// </summary>
    public IReadOnlyList<SendRequest> Taken => [.. _taken];

    /// <summary>
    /// Every message taken for an address, in the order it was taken.
    /// </summary>
    /// <param name="destination">The address the messages were for.</param>
    /// <returns>The messages.</returns>
    public IReadOnlyList<SendRequest> SentTo(EmailAddress destination) =>
        Sent(SendKind.Email, destination.Value);

    /// <summary>
    /// Every message taken for a number, in the order it was taken.
    /// </summary>
    /// <param name="destination">The number the messages were for.</param>
    /// <returns>The messages.</returns>
    public IReadOnlyList<SendRequest> SentTo(PhoneNumber destination) =>
        Sent(SendKind.Sms, destination.Value);

    /// <summary>
    /// Every message of one kind taken, in the order it was taken.
    /// </summary>
    /// <param name="kind">The message kind.</param>
    /// <returns>The messages.</returns>
    public IReadOnlyList<SendRequest> Of(MessageKind kind) =>
        [.. _taken.Where(request => request.Kind == kind)];

    /// <summary>
    /// Fails every hand-over for an address from now until <see cref="AcceptHandOversTo(EmailAddress)"/>.
    /// </summary>
    /// <param name="destination">The address whose messages are not taken.</param>
    public void FailHandOversTo(EmailAddress destination) =>
        _failing[(SendKind.Email, destination.Value)] = true;

    /// <summary>
    /// Fails every hand-over for a number from now until <see cref="AcceptHandOversTo(PhoneNumber)"/>.
    /// </summary>
    /// <param name="destination">The number whose messages are not taken.</param>
    public void FailHandOversTo(PhoneNumber destination) =>
        _failing[(SendKind.Sms, destination.Value)] = true;

    /// <summary>
    /// Takes hand-overs for an address again.
    /// </summary>
    /// <param name="destination">The address whose messages are taken.</param>
    public void AcceptHandOversTo(EmailAddress destination) =>
        _failing.TryRemove((SendKind.Email, destination.Value), out _);

    /// <summary>
    /// Takes hand-overs for a number again.
    /// </summary>
    /// <param name="destination">The number whose messages are taken.</param>
    public void AcceptHandOversTo(PhoneNumber destination) =>
        _failing.TryRemove((SendKind.Sms, destination.Value), out _);

    /// <inheritdoc/>
    public ValueTask<Result> SendAsync(SendRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (_failing.ContainsKey((request.Channel, request.Destination)))
        {
            return ValueTask.FromResult(Result.Failure(Error.From(ErrorCodes.SystemFault)));
        }

        _taken.Enqueue(request);

        return ValueTask.FromResult(Result.Success());
    }

    private SendRequest[] Sent(SendKind channel, string destination) =>
        [.. _taken.Where(request => request.Channel == channel
            && string.Equals(request.Destination, destination, StringComparison.Ordinal))];
}
