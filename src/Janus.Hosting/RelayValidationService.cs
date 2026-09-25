using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Sending;
using Janus.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Janus.Hosting;

/// <summary>
/// Reads, as the deployment starts, whether mail reaches an Apple private relay
/// address, and warns where it would not.
/// </summary>
/// <param name="scopes">Where the scope the check reads in comes from.</param>
/// <remarks>
/// Implements INT-MAIL-011 AC1 and entry 269. The warning does not stop the
/// deployment: nothing is refused while the sending domain waits to be registered. A
/// warning that could not be read or raised does, since then the one place the
/// condition would surface is gone.
/// </remarks>
internal sealed class RelayValidationService(IServiceScopeFactory scopes) : IHostedService
{
    /// <inheritdoc/>
    /// <exception cref="StartupException">
    /// The policy, the sending domain or the relay declaration could not be read, or the
    /// warning was not taken.
    /// </exception>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = scopes.CreateAsyncScope();

        (await scope.ServiceProvider
                .GetRequiredService<RelayRegistration>()
                .CheckAsync(cancellationToken)
                .ConfigureAwait(false))
            .Switch(
                () => { },
                failure => throw new StartupException(
                    "Whether mail reaches an Apple private relay address could not be read or raised.",
                    failure));
    }

    /// <inheritdoc/>
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
