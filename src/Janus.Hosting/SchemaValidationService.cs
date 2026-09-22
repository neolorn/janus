using System.Threading;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Janus.Hosting;

/// <summary>
/// Reads the migration history against the model, before the web server starts and
/// before every other check that reads a table.
/// </summary>
/// <param name="scopes">Where the scope the check reads in comes from.</param>
/// <remarks>
/// Implements OPS-MIG-002. A schema the pipeline did not migrate is found here, where
/// it names the migrations still owed, rather than at the first query against a column
/// that is not there.
/// </remarks>
internal sealed class SchemaValidationService(IServiceScopeFactory scopes) : IHostedService
{
    /// <inheritdoc/>
    /// <exception cref="StartupException">
    /// The database is behind the model by at least one migration.
    /// </exception>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = scopes.CreateAsyncScope();

        (await scope.ServiceProvider
                .GetRequiredService<SchemaValidation>()
                .ValidateAsync(cancellationToken)
                .ConfigureAwait(false))
            .Switch(
                () => { },
                failure => throw new StartupException(
                    "The database schema is behind the model; apply the migrations the details name before starting the application.",
                    failure));
    }

    /// <inheritdoc/>
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
