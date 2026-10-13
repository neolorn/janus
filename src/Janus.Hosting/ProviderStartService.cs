using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Oidc;
using Janus.Core;
using Janus.Core.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using OpenIddict.Server;

namespace Janus.Hosting;

/// <summary>
/// The provider's start: the signing keys read into the credential source, then the
/// provider's options built, before the web server starts.
/// </summary>
/// <param name="scopes">Where the scope the read happens in comes from.</param>
/// <param name="source">The signing keys, held for the rest of the process.</param>
/// <param name="options">The provider's options, built here and never rebuilt.</param>
/// <remarks>
/// Implements AUTH-KEY-001 AC7, CONV-CODE-007 and CONV-DESIGN-007. It runs once the key
/// ring is filled, since a signing key's private key is wrapped under the deployment's
/// data key. The read makes a key current where the database holds none, so the provider
/// never starts without a key, and the options, which take no <c>ValidateOnStart</c>, are
/// built after it with the credential that read left current. A deployment whose keys
/// cannot be read stops here with the code that names why, rather than answering the
/// first token request with a fault.
/// </remarks>
internal sealed class ProviderStartService(
    IServiceScopeFactory scopes,
    SigningCredentialSource source,
    IOptionsMonitor<OpenIddictServerOptions> options) : IHostedService
{
    /// <inheritdoc/>
    /// <exception cref="StartupException">The deployment's signing keys cannot be read.</exception>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using (AsyncServiceScope scope = scopes.CreateAsyncScope())
        {
            (await source
                    .ReadAsync(
                        scope.ServiceProvider.GetRequiredService<IConfigurationStore>(),
                        cancellationToken)
                    .ConfigureAwait(false))
                .Switch(
                    _ => { },
                    failure => throw new StartupException(
                        "The deployment's signing keys could not be read; the refusal the details name says why.",
                        failure));
        }

        _ = options.CurrentValue;
    }

    /// <inheritdoc/>
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
