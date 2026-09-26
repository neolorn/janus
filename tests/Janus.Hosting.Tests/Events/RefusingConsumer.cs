using System;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Hosting.Tests.Events;

/// <summary>
/// A host's consumer of registrations that refuses, or throws, for as long as a test
/// says it should.
/// </summary>
/// <remarks>Implements CONV-TEST-007: a fake, written by hand, never a mock.</remarks>
internal sealed class RefusingConsumer : IEventConsumer<AccountRegistered>
{
    /// <summary>
    /// How many more offers it refuses before it takes one.
    /// </summary>
    public int Refusals { get; set; }

    /// <summary>
    /// Whether it throws rather than answering.
    /// </summary>
    public bool Throws { get; set; }

    /// <summary>
    /// How many times it was offered an event.
    /// </summary>
    public int Offered { get; private set; }

    /// <inheritdoc/>
    public ValueTask<Result> HandleAsync(AccountRegistered raised, CancellationToken cancellationToken)
    {
        Offered++;

        if (Throws)
        {
            throw new InvalidOperationException("The consumer's own store is unreachable.");
        }

        if (Refusals > 0)
        {
            Refusals--;

            return ValueTask.FromResult(Result.Failure(Error.From(ErrorCodes.SystemFault)));
        }

        return ValueTask.FromResult(Result.Success());
    }
}
