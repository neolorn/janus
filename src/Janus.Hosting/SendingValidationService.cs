using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Sending;
using Janus.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Janus.Hosting;

/// <summary>
/// Runs the checks of the handlers the host registers, the restrictions and the
/// declared endpoints, before the web server starts.
/// </summary>
/// <param name="scopes">Where the scope the checks read in comes from.</param>
/// <remarks>
/// Implements INT-SMS-006, OPS-ALERT-003, AUTH-ABUSE-004, INT-GEN-001, INF-TLS-004 and
/// D-160. A handler the host did not register would otherwise surface at the moment a
/// person is waiting for a code.
/// </remarks>
internal sealed class SendingValidationService(IServiceScopeFactory scopes) : IHostedService
{
    /// <inheritdoc/>
    /// <exception cref="StartupException">
    /// A handler is not registered, a restriction names a supplier nothing supplies, or
    /// a declared endpoint is plaintext.
    /// </exception>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = scopes.CreateAsyncScope();

        Result outcome = await scope.ServiceProvider
            .GetRequiredService<SendingValidation>()
            .ValidateAsync(cancellationToken);

        outcome.Switch(
            () => { },
            failure => throw new StartupException(
                "The deployment's handlers, restrictions or endpoints do not hold together.",
                failure));
    }

    /// <inheritdoc/>
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
