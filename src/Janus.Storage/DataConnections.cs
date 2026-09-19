using System;
using System.Data;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Janus.Storage;

/// <summary>
/// The one accessor hand-written SQL takes its connection from. Nothing else opens a
/// connection, so a query written by hand can never miss the writes the same operation
/// has already made.
/// </summary>
/// <param name="context">The context whose connection and transaction are handed out.</param>
/// <remarks>Implements OPS-DATA-002.</remarks>
internal sealed class DataConnections(JanusDbContext context)
{
    /// <summary>
    /// Hands out the context's connection, open, with the operation's transaction
    /// attached where one is running.
    /// </summary>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The connection and the transaction to pass with every command.</returns>
    public async ValueTask<AmbientConnection> UseAsync(CancellationToken cancellationToken)
    {
        DbConnection connection = context.Database.GetDbConnection();

        if (connection.State is not ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        }

        return new AmbientConnection(connection, context.Database.CurrentTransaction?.GetDbTransaction());
    }
}
