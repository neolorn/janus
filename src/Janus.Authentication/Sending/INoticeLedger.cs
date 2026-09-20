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
    /// Whether this address may be told, recording the notice where it may.
    /// </summary>
    /// <param name="destination">The plain address, which the ledger stores hashed.</param>
    /// <param name="at">When.</param>
    /// <param name="window">One notice per address per this span.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>Whether the notice is sent rather than suppressed.</returns>
    ValueTask<bool> FirstAsync(
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
}
