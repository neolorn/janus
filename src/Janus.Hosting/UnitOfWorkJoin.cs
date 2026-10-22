using System;
using System.Data;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;

namespace Janus.Hosting;

/// <summary>
/// Joins the host's own context to the library's unit of work, so the host's writes and
/// the library's commit together at the outermost commit and roll back together.
/// </summary>
/// <remarks>
/// Implements AUTHZ-INHERIT-002, OPS-DATA-002, CONV-DESIGN-007 and CONV-LAYOUT-002
/// (D-203, D-206, D-207). It is the one place outside the connection accessor that
/// touches a context's connection: it reads the host's and its options to judge it, and
/// hands the context the accessor's connection and transaction.
/// </remarks>
public static class UnitOfWorkJoin
{
    /// <summary>
    /// Gives the host's context the open connection of a unit of work that has begun,
    /// which the context does not own, and its transaction, until the unit of work's
    /// outermost level commits or rolls back; the context then returns to the connection
    /// it was configured with. Called again for the unit of work it joined, it changes
    /// nothing.
    /// </summary>
    /// <param name="database">The host's context, over the deployment's database.</param>
    /// <param name="work">The unit of work the scope's library operations run in.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of joining.</returns>
    /// <exception cref="ArgumentNullException">The context or the unit of work is absent.</exception>
    /// <exception cref="InvalidOperationException">
    /// The unit of work has not begun; the context is configured with a connection object
    /// it owns, or was taken from a pool; the context's own connection is open or it holds
    /// a transaction; or it has joined another unit of work. Each is a fault of the calling
    /// code (CONV-ERR-001).
    /// </exception>
    public static async ValueTask UseUnitOfWorkAsync(
        this DatabaseFacade database,
        IUnitOfWork work,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(work);

        // Only the library's own unit of work can have begun a transaction to join.
        if (work is not IJoinableUnitOfWork joinable || !joinable.Begun)
        {
            throw new InvalidOperationException(
                "The unit of work has not begun, so it holds no transaction for the context to join.");
        }

        IDbContextOptions options = database.GetService<IDbContextOptions>();
        var configured = RelationalOptionsExtension.Extract(options);

        // D-207: a connection object the context owns is disposed when the context is
        // given another, so the context could never return to it.
        if (configured.Connection is not null && configured.IsConnectionOwned)
        {
            throw new InvalidOperationException(
                "The context owns the connection object it was configured with, which joining would dispose.");
        }

        // D-207: a pool's reset keeps a connection the context does not own, so the next
        // lease would run inside the unit of work's transaction.
        if (options.FindExtension<CoreOptionsExtension>()?.MaxPoolSize is not null)
        {
            throw new InvalidOperationException(
                "The context was taken from a pool, whose next lease would keep the unit of work's connection.");
        }

        // The context's connection service, read while the context is open, since the
        // unit of work may end after the scope has disposed the context.
        IRelationalConnection connection = database.GetService<IRelationalConnection>();
        AmbientConnection ambient = await joinable.ConnectionAsync(cancellationToken).ConfigureAwait(false);
        DbConnection held = connection.DbConnection;
        DbTransaction? transaction = connection.CurrentTransaction?.GetDbTransaction();

        if (ReferenceEquals(held, ambient.Connection) && ReferenceEquals(transaction, ambient.Transaction))
        {
            return;
        }

        // A context configured with a connection string or a closed connection holds
        // neither; one whose connection is open, or which holds a transaction, holds a
        // connection of its own, and so does one another unit of work gave it.
        if (held.State is not ConnectionState.Closed || transaction is not null)
        {
            throw new InvalidOperationException(
                "The context holds a connection or a transaction of its own, or has joined another unit of work.");
        }

        connection.SetDbConnection(ambient.Connection, contextOwnsConnection: false);
        joinable.AtEnd(() => Released(connection, configured));

        _ = await connection.UseTransactionAsync(ambient.Transaction, cancellationToken).ConfigureAwait(false);
    }

    // The unit of work has taken its connection and transaction back: the context lets
    // go of the transaction it was handed, which it never owned, and returns to the
    // connection it was configured with, or to the connection string or data source a
    // connection is made from where it was configured with no connection.
    private static void Released(IRelationalConnection connection, RelationalOptionsExtension configured)
    {
        connection.CurrentTransaction?.Dispose();

        if (configured.Connection is not null)
        {
            connection.SetDbConnection(configured.Connection, contextOwnsConnection: false);

            return;
        }

        connection.SetDbConnection(null, contextOwnsConnection: true);

        if (configured.ConnectionString is not null)
        {
            connection.ConnectionString = configured.ConnectionString;
        }
    }
}
