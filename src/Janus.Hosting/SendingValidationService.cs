using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Sending;
using Janus.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Janus.Hosting;

/// <summary>
/// Runs the checks of the message catalogue, the restrictions and the declared
/// endpoints, before the web server starts.
/// </summary>
/// <param name="scopes">Where the scope the checks read in comes from.</param>
/// <remarks>
/// Implements AUTH-ABUSE-005, INT-SMS-003, INT-GEN-001 and D-160. A language the
/// catalogue cannot answer in would otherwise surface at the moment a person is
/// waiting for a code.
/// </remarks>
internal sealed class SendingValidationService(IServiceScopeFactory scopes) : IHostedService
{
    /// <inheritdoc/>
    /// <exception cref="StartupException">
    /// A message is missing, a text message is over budget, a restriction names a
    /// supplier nothing supplies, or a declared endpoint is plaintext.
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
                "The deployment's messages, restrictions or endpoints do not hold together.",
                failure));
    }

    /// <inheritdoc/>
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
