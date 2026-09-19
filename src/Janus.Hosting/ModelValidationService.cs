using System.Threading;
using System.Threading.Tasks;
using Janus.Authorization.Model;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Janus.Hosting;

/// <summary>
/// Runs the checks of the model that read the database, before the web server starts.
/// </summary>
/// <param name="scopes">Where the scope the checks read in comes from.</param>
/// <remarks>
/// Implements AUTHZ-MODEL-004 and D-160. The web server is itself a hosted service and
/// hosted services start in the order they were registered, so this one is registered
/// first and the process exits before a request is served.
/// </remarks>
internal sealed class ModelValidationService(IServiceScopeFactory scopes) : IHostedService
{
    /// <inheritdoc/>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = scopes.CreateAsyncScope();

        await scope.ServiceProvider
            .GetRequiredService<ModelValidation>()
            .ValidateAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
