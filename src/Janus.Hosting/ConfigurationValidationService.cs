using System.Threading;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Privacy;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Janus.Hosting;

/// <summary>
/// Reads the declared data categories and the registration affirmation against the
/// configuration, before the web server starts.
/// </summary>
/// <param name="scopes">Where the scope the check reads in comes from.</param>
/// <remarks>
/// Implements PRIV-RET-001, PRIV-MINOR-001 and D-160. A category with no retention
/// period is found now rather than in the year an auditor asks how long it is kept.
/// </remarks>
internal sealed class ConfigurationValidationService(IServiceScopeFactory scopes) : IHostedService
{
    /// <inheritdoc/>
    /// <exception cref="StartupException">
    /// A declared data category has no retention period, or a deployment open to
    /// minors declares no written-consent basis.
    /// </exception>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using IServiceScope scope = scopes.CreateScope();

        (await scope.ServiceProvider
                .GetRequiredService<ConfigurationCoverage>()
                .ValidateAsync(cancellationToken)
                .ConfigureAwait(false))
            .Switch(
                () => { },
                failure => throw new StartupException(
                    "A data category the deployment declares has no retention period, or a deployment open to minors declares no written-consent basis.",
                    failure));
    }

    /// <inheritdoc/>
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
