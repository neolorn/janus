using System;
using System.Threading;
using System.Threading.Tasks;

namespace Janus.Authentication.Callbacks;

/// <summary>
/// What counts inbound callbacks per source, so that the endpoint answers a flood
/// before it looks anything up and a run of rejections is noticed.
/// </summary>
/// <remarks>Implements INT-GEN-003, BFF-MACH-003 and CONV-DESIGN-003.</remarks>
internal interface ICallbackLedger
{
    /// <summary>
    /// Holds one source's callbacks against every other count of them until the
    /// operation's transaction ends, so a count is taken on what is committed and two
    /// callbacks at once are counted one after the other (CONV-DESIGN-003).
    /// </summary>
    /// <param name="source">Where they came from, which the lock names only hashed.</param>
    /// <param name="cancellationToken">Abandons the wait.</param>
    /// <returns>The work of holding them.</returns>
    /// <exception cref="InvalidOperationException">No transaction is open.</exception>
    ValueTask HoldAsync(string source, CancellationToken cancellationToken);

    /// <summary>
    /// Counts one callback from a source and says how many that source has made in
    /// the window it falls in.
    /// </summary>
    /// <param name="source">Where it came from.</param>
    /// <param name="at">When.</param>
    /// <param name="window">The fixed window the count is taken over.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>How many that source has made, this one included.</returns>
    ValueTask<int> ReceivedAsync(
        string source,
        DateTimeOffset at,
        TimeSpan window,
        CancellationToken cancellationToken);

    /// <summary>
    /// Counts one rejected callback and says how many that source has had rejected
    /// since an instant.
    /// </summary>
    /// <param name="source">Where it came from.</param>
    /// <param name="at">When.</param>
    /// <param name="from">The earliest counted.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>How many were rejected, this one included.</returns>
    ValueTask<int> RejectedAsync(
        string source,
        DateTimeOffset at,
        DateTimeOffset from,
        CancellationToken cancellationToken);

    /// <summary>
    /// Forgets, under every version of the fingerprint key, each callback older than the
    /// hour its rejections are counted over (D-166, 318).
    /// </summary>
    /// <param name="now">The clock.</param>
    /// <param name="cancellationToken">Abandons the write.</param>
    /// <returns>The work of forgetting them.</returns>
    ValueTask SweepAsync(DateTimeOffset now, CancellationToken cancellationToken);
}
