using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Janus.Core;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Server;

namespace Janus.Hosting.Oidc;

/// <summary>
/// The key set a relying party validates a token against.
/// </summary>
/// <param name="oidc">Where the published keys are read.</param>
/// <remarks>
/// Implements AUTH-KEY-001. What the deployment holds is the whole of the answer: a
/// key registered with the server but not held by the deployment would be published
/// while signing nothing, so the set is replaced rather than added to.
/// </remarks>
internal sealed class KeySetAnswer(IOidc oidc)
    : IOpenIddictServerHandler<OpenIddictServerEvents.HandleJsonWebKeySetRequestContext>
{
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
