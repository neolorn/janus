using System;
using System.Threading;
using System.Threading.Tasks;

namespace Janus.Storage;

/// <summary>
/// The unit of work as a host's own context joins it: whether it has begun, the open
/// connection and transaction it runs on, and a notice when its outermost level ends.
/// </summary>
/// <remarks>
/// Implements AUTHZ-INHERIT-002, OPS-DATA-002 and CONV-DESIGN-007 (D-206). It is what
/// <c>UseUnitOfWorkAsync</c> in <c>Janus.Hosting</c> reaches, so the host's writes and the
/// library's commit together and roll back together.
/// </remarks>
internal interface IJoinableUnitOfWork
{
    /// <summary>
    /// Gets a value indicating whether a transaction is open, so there is one to join.
    /// </summary>
    bool Begun { get; }

    /// <summary>
    /// Hands out the connection the unit of work runs on, open, with its transaction
    /// attached, through the one connection accessor.
    /// </summary>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The connection and the transaction.</returns>
    /// <exception cref="InvalidOperationException">No transaction is open.</exception>
    ValueTask<AmbientConnection> ConnectionAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Registers what gives the connection and the transaction back once the outermost
    /// level has committed or rolled back, before anything registered to run after the
    /// commit runs.
    /// </summary>
    /// <param name="release">What gives them back, run once, whichever way it ended.</param>
    /// <exception cref="InvalidOperationException">No transaction is open.</exception>
    void AtEnd(Action release);
}
