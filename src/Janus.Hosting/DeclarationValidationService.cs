using System.Threading;
using System.Threading.Tasks;
using Janus.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Janus.Hosting;

/// <summary>
/// Reads what LIB-HOST-001 requires a host to declare against what it registered,
/// before the web server starts.
/// </summary>
/// <param name="scopes">Where the scope the check reads in comes from.</param>
/// <remarks>
/// Implements LIB-HOST-001, REG-PM-001 and D-160. A declaration that is not there is
/// found now rather than at the request that would have answered without it.
/// </remarks>
internal sealed class DeclarationValidationService(IServiceScopeFactory scopes) : IHostedService
{
    /// <inheritdoc/>
    /// <exception cref="StartupException">A required declaration is absent or empty.</exception>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using IServiceScope scope = scopes.CreateScope();

        (await scope.ServiceProvider
                .GetRequiredService<DeclarationCoverage>()
                .ValidateAsync(cancellationToken)
                .ConfigureAwait(false))
            .Switch(
                () => { },
                failure => throw new StartupException(
                    "A value LIB-HOST-001 requires the deployment to declare is absent.",
                    failure));
    }

    /// <inheritdoc/>
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
