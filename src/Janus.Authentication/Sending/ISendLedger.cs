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
    /// The hash of the reference of a send being judged again, whose own count and
    /// spent credit are released under the same hold before anything is read, or
    /// nothing where the send is judged for the first time.
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
    /// Counts one send from its admission, so that a failure for good can release it.
    /// </summary>
    /// <param name="reference">The hash of the send's correlation reference.</param>
    /// <param name="counted">The keys it counts against and how long each is kept.</param>
    /// <param name="spent">The keys whose granted credit this send consumes.</param>
    /// <param name="at">When it was admitted.</param>
    /// <param name="cancellationToken">Abandons the write.</param>
    /// <returns>The work of counting it.</returns>
    ValueTask RecordAsync(
        byte[] reference,
        IReadOnlyCollection<SendCount> counted,
        IReadOnlyCollection<RestrictionKey> spent,
        DateTimeOffset at,
        CancellationToken cancellationToken);

    /// <summary>
    /// Whether a send counted under a reference is still held, which a delivery report
    /// indicating delivery is checked against and changes nothing of.
    /// </summary>
    /// <param name="reference">The hash of the reference the report carried.</param>
    /// <param name="cancellationToken">Abandons the read.</param>
    /// <returns>
    /// Whether it is; false where the reference is unknown or the send is already
    /// settled.
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
    /// Takes one send back out of every bucket it counted against and gives back the
    /// credit it spent, which a send that fails for good causes: a delivery report
    /// indicating failure, attempts spent, a row removed uncarried after erasure.
    /// </summary>
    /// <param name="reference">The hash of the reference the send was counted under.</param>
    /// <param name="cancellationToken">Abandons the write.</param>
    /// <returns>
    /// Whether a send was released; false where the reference is unknown or the send
    /// is already settled.
    /// </returns>
    ValueTask<bool> ReleaseAsync(byte[] reference, CancellationToken cancellationToken);

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
