using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Oidc;
using Janus.Core;
using Janus.Hosting.Oidc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Janus.Hosting;

/// <summary>
/// Reads the key the deployment signs with, before the web server starts.
/// </summary>
/// <param name="scopes">Where the scope the read happens in comes from.</param>
/// <param name="source">What the credentials are held in for the rest of the process.</param>
/// <remarks>
/// Implements AUTH-KEY-001 and AUTH-KEY-002. The provider is put together with the key
/// the store holds and never with one of the server's own, so the key has to be there
/// before a request reaches an endpoint that signs. A deployment whose key store
/// cannot be read stops here with the code that names why, rather than answering the
/// first token request with a fault.
/// </remarks>
internal sealed class SigningKeyValidationService(
    IServiceScopeFactory scopes,
    SigningCredentialSource source) : IHostedService
{
    /// <inheritdoc/>
    /// <exception cref="StartupException">The deployment holds no key to sign with.</exception>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = scopes.CreateAsyncScope();

        (await source
                .CurrentAsync(
                    scope.ServiceProvider.GetRequiredService<SigningKeys>(),
                    cancellationToken)
                .ConfigureAwait(false))
            .Switch(
                _ => { },
                failure => throw new StartupException(
                    "The deployment holds no key to sign tokens with; the key store answered the refusal the details name.",
                    failure));
    }

    /// <inheritdoc/>
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
