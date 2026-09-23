using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;
using Microsoft.EntityFrameworkCore;

namespace Janus.Storage;

/// <summary>
/// Whether the database the application was pointed at carries the schema this build
/// of the library was compiled against, read once before anything is served.
/// </summary>
/// <param name="context">The context whose declared migrations are the model.</param>
/// <remarks>
/// Implements OPS-MIG-002. The check reads the migration history and applies nothing,
/// which is what OPS-MIG-001 leaves to the pipeline. A schema behind the model is a
/// migration the pipeline did not run and stops the application; a schema ahead of it
/// is the expand half of a rollout, which OPS-MIG-005 requires this build to run
/// against, so it is no fault at all.
/// </remarks>
internal sealed class SchemaValidation(StoreContext context)
{
    /// <summary>
    /// Reads what the database still owes the model.
    /// </summary>
    /// <param name="cancellationToken">Abandons the read.</param>
    /// <returns>Nothing, or the failure that stops startup.</returns>
    public async ValueTask<Result> ValidateAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<string> pending =
        [
            .. await context.Database
                .GetPendingMigrationsAsync(cancellationToken)
                .ConfigureAwait(false),
        ];

        return pending.Count is 0
            ? Result.Success()
            : Result.Failure(Error.From(
                ErrorCodes.StartupSchemaMismatch,
                "pending",
                JsonSerializer.SerializeToElement(pending)));
    }
}
