using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;
using Microsoft.EntityFrameworkCore;

namespace Janus.Storage;

/// <summary>
/// The library's migrations, applied to the database of the connection the context was
/// given.
/// </summary>
/// <param name="context">The context whose declared migrations are applied.</param>
/// <remarks>
/// Implements OPS-MIG-001 and CONV-DESIGN-003. Each migration applies in a transaction of
/// its own, so a migration the database refuses leaves the ones before it applied and
/// nothing of itself or after it; a migration already in the history is not applied
/// again. Nothing here reads a key or checks the schema the run creates.
/// </remarks>
internal sealed class LibraryMigrations(StoreContext context)
{
    /// <summary>
    /// Applies every migration the history does not hold.
    /// </summary>
    /// <param name="cancellationToken">Abandons the run; the migration in hand rolls back.</param>
    /// <returns>Nothing, or <c>system.fault</c> where the database refused.</returns>
    public async ValueTask<Result> ApplyAsync(CancellationToken cancellationToken)
    {
        try
        {
            await context.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);

            return Result.Success();
        }
        catch (DbException)
        {
            return Result.Failure(Error.From(ErrorCodes.SystemFault));
        }
    }
}
