using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;

namespace Janus.Hosting.Tests.Authorization;

/// <summary>
/// The last query a context sent, with the values it carried, for a test that asks
/// the database how it would run a statement the library writes (OPS-DB-003 AC2).
/// </summary>
internal sealed class CapturedCommand : DbCommandInterceptor
{
    /// <summary>
    /// The statement as it was sent.
    /// </summary>
    public string Text { get; private set; } = string.Empty;

    /// <summary>
    /// The values it was sent with, copied so they outlive the command.
    /// </summary>
    public IReadOnlyList<NpgsqlParameter> Parameters { get; private set; } = [];

    /// <inheritdoc/>
    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        Text = command.CommandText;
        Parameters = [.. command.Parameters.Cast<NpgsqlParameter>().Select(parameter => parameter.Clone())];

        return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
    }
}
