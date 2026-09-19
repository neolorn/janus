using System;
using Janus.Core;

namespace Janus.Privacy.Erasures;

/// <summary>
/// What is left of an erasure once the library's own work has committed: the host-side
/// work still outstanding.
/// </summary>
/// <remarks>
/// Implements IDN-LIFE-003b and chapter 10 sections 5.12 and 5.12a. The row is written
/// in the same transaction as the state change, the overwrite of the subject's wrapped
/// key and the neutralisation of its fingerprints, so it never describes a step of the
/// library's that might not have happened: if the row exists, all of them did.
/// </remarks>
internal sealed class Erasure
{
    private Erasure(SubjectId subject, DateTimeOffset requestedAt, ErasureReason reason)
    {
        Subject = subject;
        RequestedAt = requestedAt;
        Reason = reason;
        Status = ErasureStatus.AwaitingSubscribers;
    }

    /// <summary>
    /// Whose erasure this is.
    /// </summary>
    public SubjectId Subject { get; }

    /// <summary>
    /// When it was requested.
    /// </summary>
    public DateTimeOffset RequestedAt { get; }

    /// <summary>
    /// Why it happened.
    /// </summary>
    public ErasureReason Reason { get; }

    /// <summary>
    /// How far the host-side work has got.
    /// </summary>
    public ErasureStatus Status { get; private set; }

    /// <summary>
    /// How many delivery attempts have been made across subscribers.
    /// </summary>
    public int Attempts { get; private set; }

    /// <summary>
    /// An erasure that has just committed. Nothing of the library's is outstanding;
    /// what remains is every registered subscriber confirming.
    /// </summary>
    /// <param name="subject">Whose it is.</param>
    /// <param name="requestedAt">When it was requested.</param>
    /// <param name="reason">Why it happened.</param>
    /// <returns>The erasure.</returns>
    public static Erasure Begun(SubjectId subject, DateTimeOffset requestedAt, ErasureReason reason) =>
        new(subject, requestedAt, reason);

    /// <summary>
    /// The erasure as it already stands. This is the store's translation of a stored
    /// row and no progress that was just made.
    /// </summary>
    /// <param name="subject">Whose it is.</param>
    /// <param name="requestedAt">When it was requested.</param>
    /// <param name="reason">Why it happened.</param>
    /// <param name="status">How far the host-side work has got.</param>
    /// <param name="attempts">How many attempts have been made.</param>
    /// <returns>The erasure.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The attempts are negative.</exception>
    public static Erasure Existing(
        SubjectId subject,
        DateTimeOffset requestedAt,
        ErasureReason reason,
        ErasureStatus status,
        int attempts)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(attempts);

        return new Erasure(subject, requestedAt, reason)
        {
            Status = status,
            Attempts = attempts,
        };
    }

    /// <summary>
    /// Counts one delivery attempt across the subscribers.
    /// </summary>
    /// <exception cref="InvalidOperationException">The erasure is no longer outstanding.</exception>
    public void RecordAttempt()
    {
        Outstanding();

        Attempts++;
    }

    /// <summary>
    /// Every required subscriber confirmed.
    /// </summary>
    /// <exception cref="InvalidOperationException">The erasure is no longer outstanding.</exception>
    public void Complete()
    {
        Outstanding();

        Status = ErasureStatus.Complete;
    }

    /// <summary>
    /// A subscriber exhausted its retries, so the manual completion path is pending.
    /// </summary>
    /// <exception cref="InvalidOperationException">The erasure is no longer outstanding.</exception>
    public void Fail()
    {
        Outstanding();

        Status = ErasureStatus.Failed;
    }

    /// <summary>
    /// The manual completion path ran and closed the erasure.
    /// </summary>
    /// <exception cref="InvalidOperationException">The erasure was never failed.</exception>
    public void CompleteManually()
    {
        if (Status is not ErasureStatus.Failed)
        {
            throw new InvalidOperationException(
                "The manual completion path closes an erasure that failed.");
        }

        Status = ErasureStatus.Complete;
    }

    private void Outstanding()
    {
        if (Status is not ErasureStatus.AwaitingSubscribers)
        {
            throw new InvalidOperationException("The erasure is no longer awaiting subscribers.");
        }
    }
}
