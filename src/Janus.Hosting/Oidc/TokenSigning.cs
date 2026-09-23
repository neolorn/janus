using System;
using System.Threading.Tasks;
using Janus.Authentication.Oidc;
using Janus.Core;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Server;

namespace Janus.Hosting.Oidc;

/// <summary>
/// What the server signs a token with.
/// </summary>
/// <param name="source">What the deployment signs with now.</param>
/// <param name="keys">Where the key signing now is read.</param>
/// <remarks>
/// Implements AUTH-KEY-001. The key is read for each request rather than fixed when
/// the server was put together, so a rotation another process performed takes effect
/// here without a restart (AUTH-KEY-001 AC1).
/// </remarks>
internal sealed class TokenSigning(SigningCredentialSource source, SigningKeys keys)
    : IOpenIddictServerHandler<OpenIddictServerEvents.GenerateTokenContext>
{
    /// <summary>
    /// Where the handler sits: after the server has attached the credentials its
    /// options hold, which this replaces with the key the store holds now.
    /// </summary>
    public const int Order = int.MinValue + 100_500;

    /// <inheritdoc/>
    public async ValueTask HandleAsync(OpenIddictServerEvents.GenerateTokenContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        Error? failure = null;

        SigningCredentials credentials = (await source
                .CurrentAsync(keys, context.CancellationToken)
                .ConfigureAwait(false))
            .Match(signing => signing, error => Withheld(error, ref failure));

        if (failure is not null)
        {
            throw new InvalidOperationException("The deployment holds no key to sign with.");
        }

        context.SigningCredentials = credentials;
    }

    private static SigningCredentials Withheld(Error error, ref Error? failure)
    {
        failure = error;

        return default!;
    }
}
