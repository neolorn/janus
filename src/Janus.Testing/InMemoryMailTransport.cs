using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Testing;

/// <summary>
/// A mail transport that keeps every mail it takes, readable by destination, and fails a
/// delivery only where the test says so.
/// </summary>
/// <remarks>
/// Implements LIB-TEST-003. A delivery that fails is not kept, and the library retries
/// it as it retries any transport's failure.
/// </remarks>
public sealed class InMemoryMailTransport : IMailTransport
{
    private readonly ConcurrentQueue<MailMessage> _sent = new();

    private readonly ConcurrentDictionary<EmailAddress, bool> _failing = new();

    /// <summary>
    /// Every mail taken for a destination, in the order it was taken.
    /// </summary>
    /// <param name="destination">The address the mails were sent to.</param>
    /// <returns>The mails.</returns>
    public IReadOnlyList<MailMessage> SentTo(EmailAddress destination) =>
        [.. _sent.Where(mail => mail.Destination == destination)];

    /// <summary>
    /// Fails every delivery to a destination from now until <see cref="AcceptDeliveriesTo"/>.
    /// </summary>
    /// <param name="destination">The address whose deliveries fail.</param>
    public void FailDeliveriesTo(EmailAddress destination) => _failing[destination] = true;

    /// <summary>
    /// Takes deliveries to a destination again.
    /// </summary>
    /// <param name="destination">The address whose deliveries are taken.</param>
    public void AcceptDeliveriesTo(EmailAddress destination) => _failing.TryRemove(destination, out _);

    /// <inheritdoc/>
    public ValueTask<Result> SendAsync(MailMessage mail, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(mail);

        if (_failing.ContainsKey(mail.Destination))
        {
            return ValueTask.FromResult(Result.Failure(Error.From(ErrorCodes.SystemFault)));
        }

        _sent.Enqueue(mail);

        return ValueTask.FromResult(Result.Success());
    }
}
