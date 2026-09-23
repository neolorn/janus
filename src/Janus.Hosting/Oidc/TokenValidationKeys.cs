using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Janus.Core;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Server;

namespace Janus.Hosting.Oidc;

/// <summary>
/// What the server validates a token it wrote against.
/// </summary>
/// <param name="oidc">Where the published keys are read.</param>
/// <remarks>
/// Implements AUTH-KEY-001 AC2. A token outlives the key that signed it by the
/// overlap, so what validates it is the set the deployment publishes and not the one
/// key it is signing with now.
/// </remarks>
internal sealed class TokenValidationKeys(IOidc oidc)
    : IOpenIddictServerHandler<OpenIddictServerEvents.ValidateTokenContext>
{
    /// <summary>
    /// Where the handler sits: after the server has settled what it would validate a
    /// token against, and before it reads the value itself.
    /// </summary>
    public const int Order = int.MinValue + 102_500;

    /// <inheritdoc/>
    public async ValueTask HandleAsync(OpenIddictServerEvents.ValidateTokenContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        Error? failure = null;

        IReadOnlyList<PublishedSigningKey> published = (await oidc
                .KeysAsync(context.CancellationToken)
                .ConfigureAwait(false))
            .Match(keys => keys, error => Withheld(error, ref failure));

        if (failure is not null)
        {
            return;
        }

        JsonWebKey[] set = [.. published.Select(PublishedKeys.Of)];

        context.TokenValidationParameters.IssuerSigningKeys = set;
        context.TokenValidationParameters.IssuerSigningKeyResolver = (_, _, _, _) => set;
    }

    private static IReadOnlyList<PublishedSigningKey> Withheld(Error error, ref Error? failure)
    {
        failure = error;

        return default!;
    }
}
