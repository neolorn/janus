using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Janus.Hosting.Tests.Authorization;

/// <summary>
/// How many statements a context sent, for a test that is about the cost of a page
/// rather than about its answer (AUTHZ-GATE-005 AC1).
/// </summary>
internal sealed class CountedCommands : DbCommandInterceptor
{
    /// <summary>
    /// The statements sent since the context was opened.
    /// </summary>
    public int Statements { get; private set; }

    /// <inheritdoc/>
    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        Statements++;

        return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
    }
}
