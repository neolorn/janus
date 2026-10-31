using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Janus.Authentication.Sending;

/// <summary>
/// Where what has been sent is counted. The plain key value never reaches a record:
/// the ledger keeps its keyed hash and the times, and deletes the record once its
/// buckets are empty (AUTH-ABUSE-004).
/// </summary>
/// <remarks>Implements AUTH-ABUSE-004, INT-SMS-005 and CONV-DESIGN-003.</remarks>
internal interface ISendLedger
{
    /// <summary>
    /// Takes the counter of every key a send is judged on and holds it to the end of
    /// the transaction in progress, creating one first where none stands, and answers
    /// what stands against each. The counters are taken in one fixed order, so two
    /// sends judged at once are judged one after the other. Every record that decides
    /// nothing is deleted first, as <see cref="SweepAsync"/> deletes it.
    /// </summary>
    /// <param name="keys">The keys.</param>
    /// <param name="stale">
    /// The instants before which a time decides nothing, for the destinations and for
    /// every other key.
    /// </param>
    /// <param name="setAside">
    /// The hash of the reference of a message being judged again, whose own count and
    /// spent credit are released under the same hold before anything is read, and whose
    /// send's one count against a restriction marked <c>resend</c> is left out of what
    /// is answered, or nothing where the send is judged for the first time.
    /// </param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The counter of each key, empty where nothing has been counted.</returns>
    ValueTask<IReadOnlyDictionary<RestrictionKey, SendCounter>> HoldAsync(
        IReadOnlyCollection<RestrictionKey> keys,
        CounterStaleness stale,
        byte[]? setAside,
        CancellationToken cancellationToken);

    /// <summary>
    /// Deletes every destination record whose newest time is older than the
    /// destinations' instant, every other record whose newest time is older than the
    /// keys' instant, and every record that holds no time, so a record goes once it
    /// decides nothing whether or not the key is sent to again (PRIV-RET-005 AC2). A
    /// record another transaction holds is left to the next sweep.
    /// </summary>
    /// <param name="stale">The two instants.</param>
    /// <param name="cancellationToken">Abandons the deletes.</param>
    /// <returns>The work of deleting them.</returns>
    ValueTask SweepAsync(CounterStaleness stale, CancellationToken cancellationToken);

    /// <summary>
    /// Counts the messages of one send from their admission, so that a failure for good
    /// can release each, and counts the send once against the keys of the restrictions
    /// marked <c>resend</c>. The messages are every message of a send at its first
    /// admission, the first of them naming the send, or the one message of a send
    /// judged again, which stays of the send it was first counted under: what that send
    /// counted once is taken back and counted at this instant in its place.
    /// </summary>
    /// <param name="messages">The messages, at least one, each with what it counts against and spends.</param>
    /// <param name="once">What the send counts against and spends once.</param>
    /// <param name="secondFactor">Whether the send's purpose is <c>secondfactor</c>.</param>
    /// <param name="at">When it was admitted.</param>
    /// <param name="cancellationToken">Abandons the write.</param>
    /// <returns>The work of counting them.</returns>
    ValueTask RecordAsync(
        IReadOnlyList<CountedMessage> messages,
        SendPlan once,
        bool secondFactor,
        DateTimeOffset at,
        CancellationToken cancellationToken);

    /// <summary>
    /// Takes one message's count back out of every bucket it counted against and gives
    /// back the credit it spent, while the message still waits to be judged again: it
    /// stays of its send, and what the send counted once stands.
    /// </summary>
    /// <param name="reference">The hash of the reference the message was counted under.</param>
    /// <param name="cancellationToken">Abandons the write.</param>
    /// <returns>The work of setting it aside.</returns>
    ValueTask SetAsideAsync(byte[] reference, CancellationToken cancellationToken);

    /// <summary>
    /// Whether a message counted under a reference still holds the count of an
    /// admission, which is what lets its first attempt carry it without judging it again.
    /// </summary>
    /// <param name="reference">The hash of the reference it was counted under.</param>
    /// <param name="cancellationToken">Abandons the read.</param>
    /// <returns>
    /// Whether it does; false where the reference is unknown, the message is already
    /// settled or its count was set aside.
    /// </returns>
    ValueTask<bool> HoldsAsync(byte[] reference, CancellationToken cancellationToken);

    /// <summary>
    /// Forgets, under every version of the fingerprint key, each send whose settling time
    /// has passed, which no delivery report can take back (D-166, 318).
    /// </summary>
    /// <param name="now">The clock.</param>
    /// <param name="cancellationToken">Abandons the write.</param>
    /// <returns>The work of forgetting them.</returns>
    ValueTask SweepSettledAsync(DateTimeOffset now, CancellationToken cancellationToken);

    /// <summary>
    /// Takes one message back out of every bucket it counted against and gives back the
    /// credit it spent, which a message that fails for good causes: a delivery report
    /// indicating failure, attempts spent, a row removed uncarried after erasure, a
    /// retry refused. Where it was the last message of its send still counted, every
    /// message of the send has failed for good, and what the send counted once against
    /// the restrictions marked <c>resend</c> is taken back with it. The message stays
    /// known, released, until its send settles, and releasing it again changes nothing
    /// (D-227).
    /// </summary>
    /// <param name="reference">The hash of the reference the message was counted under.</param>
    /// <param name="cancellationToken">Abandons the write.</param>
    /// <returns>
    /// Whether a counted message answers to the reference, released already or not;
    /// false where the reference is unknown or the send is already settled.
    /// </returns>
    ValueTask<bool> ReleaseAsync(byte[] reference, CancellationToken cancellationToken);

    /// <summary>
    /// Takes a report that a message was delivered: where the message is of a send of
    /// purpose <c>secondfactor</c>, what the send counted once against the restrictions
    /// marked <c>resend</c> is taken back, and nothing else changes for any send; for a
    /// message already released nothing changes (D-227).
    /// </summary>
    /// <param name="reference">The hash of the reference the report carried.</param>
    /// <param name="cancellationToken">Abandons the write.</param>
    /// <returns>
    /// Whether a counted message answers to the reference, released already or not;
    /// false where it is unknown or the message is already settled.
    /// </returns>
    ValueTask<bool> DeliveredAsync(byte[] reference, CancellationToken cancellationToken);

    /// <summary>
    /// Names what a send is answered by, the code it carries presented right or its
    /// link pressed, so that the answer can release what the send counted once.
    /// </summary>
    /// <param name="reference">The hash of the reference the send was admitted under.</param>
    /// <param name="answer">The hash that names the code or the link the send carries.</param>
    /// <param name="cancellationToken">Abandons the write.</param>
    /// <returns>The work of naming it.</returns>
    ValueTask BindAsync(byte[] reference, byte[] answer, CancellationToken cancellationToken);

    /// <summary>
    /// Takes back what every send answered by a code or a link counted once against the
    /// restrictions marked <c>resend</c>, and nothing else; a code or a link no counted
    /// send carries changes nothing.
    /// </summary>
    /// <param name="answer">The hash that names the code presented right or the link pressed.</param>
    /// <param name="cancellationToken">Abandons the write.</param>
    /// <returns>The work of releasing it.</returns>
    ValueTask AnsweredAsync(byte[] answer, CancellationToken cancellationToken);

    /// <summary>
    /// Adds credit to one key, which support does for a person an attacker or a loop
    /// has exhausted.
    /// </summary>
    /// <param name="key">The restriction and the value.</param>
    /// <param name="credit">How many sends the credit is worth.</param>
    /// <param name="cancellationToken">Abandons the write.</param>
    /// <returns>The work of adding it.</returns>
    ValueTask GrantAsync(RestrictionKey key, int credit, CancellationToken cancellationToken);
}
