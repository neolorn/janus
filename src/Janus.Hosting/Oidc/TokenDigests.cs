using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Janus.Authentication.Oidc;
using Janus.Core;
using Janus.Core.Configuration;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Abstractions;
using OpenIddict.Server;

namespace Janus.Hosting.Oidc;

/// <summary>
/// The hashes of the access token and of the code that the identity token carries
/// (<c>at_hash</c> and <c>c_hash</c>), under the hash of the algorithm the current key
/// signs the identity token with.
/// </summary>
/// <param name="source">The signing keys.</param>
/// <param name="configuration">Where the cadence is read.</param>
/// <remarks>
/// Implements AUTH-KEY-001 and CONV-CODE-007. It stands in the place of the server's own
/// step, which picks the hash from the signing credentials its options hold and is
/// removed, so no step of the provider takes a signing credential from its options
/// (D-181). The hash is the one the server's own step computes for ES256, the one
/// algorithm the deployment signs with: the left-most half of the SHA-256 of the token's
/// ASCII octets, base64url-encoded.
/// </remarks>
internal sealed class TokenDigests(SigningCredentialSource source, IConfigurationStore configuration)
    : IOpenIddictServerHandler<OpenIddictServerEvents.ProcessSignInContext>
{
    /// <summary>
    /// Where the handler sits: the place of the server's own step it replaces.
    /// </summary>
    public static int Order => OpenIddictServerHandlers.AttachTokenDigests.Descriptor.Order;

    /// <inheritdoc/>
    public async ValueTask HandleAsync(OpenIddictServerEvents.ProcessSignInContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.IdentityTokenPrincipal is null)
        {
            throw new InvalidOperationException("The identity token has no principal to carry the hashes.");
        }

        if (string.IsNullOrEmpty(context.AccessToken) && string.IsNullOrEmpty(context.AuthorizationCode))
        {
            return;
        }

        Error? failure = null;

        SigningKeySet set = (await source
                .ReadAsync(configuration, context.CancellationToken)
                .ConfigureAwait(false))
            .Match(read => read, error => Withheld(error, ref failure));

        if (failure is not null)
        {
            throw new InvalidOperationException("The deployment's signing keys could not be read: " + failure.Code);
        }

        if (!string.Equals(set.Signing.Algorithm, SecurityAlgorithms.EcdsaSha256, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The signing algorithm names no hash this version computes.");
        }

        if (!string.IsNullOrEmpty(context.AccessToken))
        {
            _ = context.IdentityTokenPrincipal.SetClaim(
                OpenIddictConstants.Claims.AccessTokenHash,
                Digest(context.AccessToken));
        }

        if (!string.IsNullOrEmpty(context.AuthorizationCode))
        {
            _ = context.IdentityTokenPrincipal.SetClaim(
                OpenIddictConstants.Claims.CodeHash,
                Digest(context.AuthorizationCode));
        }
    }

    private static string Digest(string token)
    {
        byte[] digest = SHA256.HashData(Encoding.ASCII.GetBytes(token));

        return Base64UrlEncoder.Encode(digest, 0, digest.Length / 2);
    }

    private static SigningKeySet Withheld(Error error, ref Error? failure)
    {
        failure = error;

        return default!;
    }
}
