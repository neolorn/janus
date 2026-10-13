using System;
using System.Linq;
using System.Threading.Tasks;
using Janus.Authentication.Oidc;
using Janus.Core;
using Janus.Core.Configuration;
using OpenIddict.Server;

namespace Janus.Hosting.Oidc;

/// <summary>
/// The signing algorithms the discovery document advertises for the identity token: those
/// of the keys the key set publishes.
/// </summary>
/// <param name="source">The signing keys.</param>
/// <param name="configuration">Where the cadence is read.</param>
/// <param name="time">The clock the deployment runs on.</param>
/// <remarks>
/// Implements AUTH-KEY-001 and CONV-CODE-007. It stands in the place of the server's own
/// step, which reads the signing credentials its options hold and is removed, so no step
/// of the provider takes a signing credential from its options (D-181).
/// </remarks>
internal sealed class SigningAlgorithms(
    SigningCredentialSource source,
    IConfigurationStore configuration,
    TimeProvider time)
    : IOpenIddictServerHandler<OpenIddictServerEvents.HandleConfigurationRequestContext>
{
    /// <summary>
    /// Where the handler sits: the place of the server's own step it replaces.
    /// </summary>
    public static int Order => OpenIddictServerHandlers.Discovery.AttachSigningAlgorithms.Descriptor.Order;

    /// <inheritdoc/>
    public async ValueTask HandleAsync(OpenIddictServerEvents.HandleConfigurationRequestContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        Error? failure = null;

        SigningKeySet set = (await source
                .ReadAsync(configuration, context.CancellationToken)
                .ConfigureAwait(false))
            .Match(read => read, error => Withheld(error, ref failure));

        if (failure is not null)
        {
            throw new InvalidOperationException("The deployment's signing keys could not be read: " + failure.Code);
        }

        foreach (string algorithm in set.Published(time.GetUtcNow())
            .Select(held => held.Key.Algorithm)
            .Distinct(StringComparer.Ordinal))
        {
            context.IdTokenSigningAlgorithms.Add(algorithm);
        }
    }

    private static SigningKeySet Withheld(Error error, ref Error? failure)
    {
        failure = error;

        return default!;
    }
}
