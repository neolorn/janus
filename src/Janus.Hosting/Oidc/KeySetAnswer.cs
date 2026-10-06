using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Janus.Authentication.Oidc;
using Janus.Core;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Server;

namespace Janus.Hosting.Oidc;

/// <summary>
/// The key set a relying party validates a token against: the next key, the current key
/// and each replaced key within its overlap.
/// </summary>
/// <param name="oidc">Where the published keys are read.</param>
/// <remarks>
/// Implements AUTH-KEY-001 AC3 and AC8 and CONV-CODE-007. It stands in the place of the
/// server's own step, which publishes the signing keys its options hold and is removed,
/// so the key the options hold from the start is published only while the set says so,
/// and a request for the key set never reads a private key object, disposed or not.
/// </remarks>
internal sealed class KeySetAnswer(IOidc oidc)
    : IOpenIddictServerHandler<OpenIddictServerEvents.HandleJsonWebKeySetRequestContext>
{
    /// <summary>
    /// Where the handler sits: the place of the server's own step it replaces.
    /// </summary>
    public static int Order => OpenIddictServerHandlers.Discovery.AttachSigningKeys.Descriptor.Order;

    /// <inheritdoc/>
    public async ValueTask HandleAsync(
        OpenIddictServerEvents.HandleJsonWebKeySetRequestContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        Error? failure = null;

        IReadOnlyList<PublishedSigningKey> published = (await oidc
                .KeysAsync(context.CancellationToken)
                .ConfigureAwait(false))
            .Match(keys => keys, error => Withheld(error, ref failure));

        if (failure is not null)
        {
            throw new InvalidOperationException("The deployment's signing keys could not be read.");
        }

        context.Keys.Clear();

        foreach (PublishedSigningKey key in published)
        {
            context.Keys.Add(PublishedKeys.Of(key));
        }
    }

    private static IReadOnlyList<PublishedSigningKey> Withheld(Error error, ref Error? failure)
    {
        failure = error;

        return default!;
    }
}
