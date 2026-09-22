using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Oidc;
using Janus.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Janus.Hosting;

/// <summary>
/// Reads the client registry and the configured default destination, before the web
/// server starts.
/// </summary>
/// <param name="scopes">Where the scope the check reads in comes from.</param>
/// <remarks>
/// Implements API-REDIR-001 and D-160. A destination that will not resolve is found
/// at startup rather than at the moment a person has finished registering and is
/// waiting to be sent back.
/// </remarks>
internal sealed class RedirectValidationService(IServiceScopeFactory scopes) : IHostedService
{
    /// <inheritdoc/>
    /// <exception cref="StartupException">
    /// A registered client's destination is not an absolute origin, or the configured
    /// default names no registered browser application.
    /// </exception>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = scopes.CreateAsyncScope();

        (await scope.ServiceProvider
                .GetRequiredService<RedirectValidation>()
                .ValidateAsync(cancellationToken)
                .ConfigureAwait(false))
            .Switch(
                () => { },
                failure => throw new StartupException(
                    "A registered client's return destination is not an absolute origin, or the configured default names no registered browser application.",
                    failure));
    }

    /// <inheritdoc/>
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
