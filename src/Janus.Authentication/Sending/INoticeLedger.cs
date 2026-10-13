using System;
using System.Threading;
using System.Threading.Tasks;

namespace Janus.Authentication.Sending;

/// <summary>
/// What remembers which addresses have already been told there is no account, so
/// that a distributed attacker cannot make the deployment post that sentence to
/// arbitrary mailboxes at scale.
/// </summary>
/// <remarks>Implements AUTH-ABUSE-003, REG-SESS-005 and CONV-DESIGN-003.</remarks>
internal interface INoticeLedger
{
    /// <summary>
    /// Holds one address's notices against every other notice to it until the
    /// operation's transaction ends, so whether it was told is read as committed and two
    /// asks at once tell it once (CONV-DESIGN-003).
    /// </summary>
    /// <param name="destination">The plain address, which the lock names only hashed.</param>
    /// <param name="cancellationToken">Abandons the wait.</param>
    /// <returns>The work of holding it.</returns>
    /// <exception cref="InvalidOperationException">No transaction is open.</exception>
    ValueTask HoldAsync(string destination, CancellationToken cancellationToken);

    /// <summary>
    /// Whether this address was told inside the window, under any version of the
    /// fingerprint key. It records nothing.
    /// </summary>
    /// <param name="destination">The plain address, which the ledger stores hashed.</param>
    /// <param name="at">When.</param>
    /// <param name="window">One notice per address per this span.</param>
    /// <param name="cancellationToken">Abandons the read.</param>
    /// <returns>Whether a notice to it is suppressed rather than sent.</returns>
    ValueTask<bool> WasToldAsync(
        string destination,
        DateTimeOffset at,
        TimeSpan window,
        CancellationToken cancellationToken);

    /// <summary>
    /// Marks the address as told, which spends its window. The caller writes the mark
    /// only once the notice's send is admitted, so a refused notice spends no window
    /// (AUTH-ABUSE-003, D-188).
    /// </summary>
    /// <param name="destination">The plain address, which the ledger stores hashed.</param>
    /// <param name="at">When.</param>
    /// <param name="window">One notice per address per this span.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of marking it.</returns>
    ValueTask MarkAsync(
        string destination,
        DateTimeOffset at,
        TimeSpan window,
        CancellationToken cancellationToken);

    /// <summary>
    /// How many such notices went out across the deployment since one instant.
    /// </summary>
    /// <param name="from">The earliest counted.</param>
    /// <param name="cancellationToken">Abandons the read.</param>
    /// <returns>The count.</returns>
    ValueTask<int> SinceAsync(DateTimeOffset from, CancellationToken cancellationToken);

    /// <summary>
    /// Forgets, under every version of the fingerprint key, each notice older than both
    /// the window and the hour the probe alert counts over (D-166, 318).
    /// </summary>
    /// <param name="now">The clock.</param>
    /// <param name="window">How long one address is told once.</param>
    /// <param name="cancellationToken">Abandons the write.</param>
    /// <returns>The work of forgetting them.</returns>
    ValueTask SweepAsync(DateTimeOffset now, TimeSpan window, CancellationToken cancellationToken);
}
