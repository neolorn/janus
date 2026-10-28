using System;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Authentication.Records;

/// <summary>
/// Where the offer to attach each account session holds is kept, one to a session.
/// </summary>
/// <remarks>Implements REC-ATT-001 and CONV-DESIGN-003.</remarks>
internal interface IRecordOfferStore
{
    /// <summary>
    /// The offer a session holds.
    /// </summary>
    /// <param name="session">The session.</param>
    /// <param name="cancellationToken">Abandons the read.</param>
    /// <returns>The offer, or nothing where the session holds none.</returns>
    ValueTask<OfferToAttach?> FindAsync(SessionId session, CancellationToken cancellationToken);

    /// <summary>
    /// The offer a session holds, read under its row's lock inside the operation's
    /// transaction.
    /// </summary>
    /// <param name="session">The session.</param>
    /// <param name="cancellationToken">Abandons the read.</param>
    /// <returns>The offer, or nothing where the session holds none.</returns>
    ValueTask<OfferToAttach?> FindForUpdateAsync(SessionId session, CancellationToken cancellationToken);

    /// <summary>
    /// Holds an offer on its session, in place of any it held.
    /// </summary>
    /// <param name="offer">The offer.</param>
    /// <param name="cancellationToken">Abandons the write.</param>
    /// <returns>The work of holding it.</returns>
    ValueTask PutAsync(OfferToAttach offer, CancellationToken cancellationToken);

    /// <summary>
    /// Ends the offer a session holds, where it holds one.
    /// </summary>
    /// <param name="session">The session.</param>
    /// <param name="cancellationToken">Abandons the write.</param>
    /// <returns>The work of ending it.</returns>
    ValueTask RemoveAsync(SessionId session, CancellationToken cancellationToken);

    /// <summary>
    /// Removes every offer whose life has run out.
    /// </summary>
    /// <param name="now">The instant judged against.</param>
    /// <param name="cancellationToken">Abandons the sweep.</param>
    /// <returns>How many were removed.</returns>
    ValueTask<int> SweepAsync(DateTimeOffset now, CancellationToken cancellationToken);
}
