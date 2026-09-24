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
    /// What stands against each key a send would count under. Every record whose
    /// newest time is older than the given instant is deleted first, so a record that
    /// decides nothing is never read and never stands in the table.
    /// </summary>
    /// <param name="keys">The keys.</param>
    /// <param name="stale">
    /// The instant before which a time decides nothing: the clock less the longest
    /// interval any restriction now declares.
    /// </param>
    /// <param name="cancellationToken">Abandons the read.</param>
    /// <returns>The counter of each key, absent where nothing has been counted.</returns>
    ValueTask<IReadOnlyDictionary<RestrictionKey, SendCounter>> CountersAsync(
        IReadOnlyCollection<RestrictionKey> keys,
        DateTimeOffset stale,
        CancellationToken cancellationToken);

    /// <summary>
    /// Counts one send that the transport took, so a delivery report can release it.
    /// </summary>
    /// <param name="reference">The hash of the send's correlation reference.</param>
    /// <param name="counted">The keys it counts against and how long each is kept.</param>
    /// <param name="spent">The keys whose granted credit this send consumes.</param>
    /// <param name="at">When it was sent.</param>
    /// <param name="cancellationToken">Abandons the write.</param>
    /// <returns>The work of counting it.</returns>
    ValueTask RecordAsync(
        byte[] reference,
        IReadOnlyCollection<SendCount> counted,
        IReadOnlyCollection<RestrictionKey> spent,
        DateTimeOffset at,
        CancellationToken cancellationToken);

    /// <summary>
    /// Whether a send the transport took under a reference is still held, which a
    /// delivery report indicating delivery is checked against and changes nothing of.
    /// </summary>
    /// <param name="reference">The hash of the reference the report carried.</param>
    /// <param name="cancellationToken">Abandons the read.</param>
    /// <returns>
    /// Whether it is; false where the reference is unknown or the send is already
    /// settled.
    /// </returns>
    ValueTask<bool> HoldsAsync(byte[] reference, CancellationToken cancellationToken);

    /// <summary>
    /// Takes one send back out of every bucket it counted against, which a delivery
    /// report indicating failure causes and nothing else does.
    /// </summary>
    /// <param name="reference">The hash of the reference the report carried.</param>
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
