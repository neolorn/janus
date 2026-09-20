using System;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Janus.Authentication.Oidc;
using Janus.Core;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Abstractions;
using OpenIddict.Server;

namespace Janus.Hosting.Oidc;

/// <summary>
/// What the server writes out for a code and a refresh token, and what it signs an
/// access token and an identity token with.
/// </summary>
/// <param name="keys">The key signing now.</param>
/// <remarks>
/// Implements AUTH-SESS-012, AUTH-OIDC-003 and AUTH-KEY-001. A code and a refresh
/// token are the library's own opaque values, held by what they hash to, so the server
/// writes out what the library issued rather than a token of its own; the key an
/// access token is signed with is read for each request, so a rotation takes effect
/// without a restart.
/// </remarks>
internal sealed class TokenFormat(SigningKeys keys)
    : IOpenIddictServerHandler<OpenIddictServerEvents.GenerateTokenContext>, IDisposable
{
    private ECDsa? _signing;

    /// <summary>
    /// Where the handler sits: after the server has attached the credentials it holds,
    /// which this replaces, and before it would write a token of its own.
    /// </summary>
    public const int Order = int.MinValue + 100_500;

    /// <inheritdoc/>
    public async ValueTask HandleAsync(OpenIddictServerEvents.GenerateTokenContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (Carried(context) is string issued)
        {
            context.Token = issued;

            return;
        }

        Error? failure = null;

        SigningMaterial material = (await keys
                .SigningAsync(context.CancellationToken)
                .ConfigureAwait(false))
            .Match(signing => signing, error => Withheld(error, ref failure));

        if (failure is not null)
        {
            throw new InvalidOperationException("The deployment holds no key to sign with.");
        }

        context.SigningCredentials = Credentials(material);
    }

    /// <inheritdoc/>
    public void Dispose() => _signing?.Dispose();

    private static SigningMaterial Withheld(Error error, ref Error? failure)
    {
        failure = error;

        return default!;
    }

    // The key has to outlive the handler's own call, because the server signs with it
    // afterwards; the request's scope disposes it.
    private SigningCredentials Credentials(SigningMaterial material)
    {
        _signing?.Dispose();
        _signing = ECDsa.Create();

        try
        {
            _signing.ImportPkcs8PrivateKey(material.PrivateKey, out _);

            var key = new ECDsaSecurityKey(_signing) { KeyId = material.KeyId };

            // The key is read again for the next request and this one goes with the
            // scope, so what signs with it is built for the signature and released
            // after it rather than held against the key identifier.
            key.CryptoProviderFactory = new CryptoProviderFactory { CacheSignatureProviders = false };

            return new SigningCredentials(key, SecurityAlgorithms.EcdsaSha256);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(material.PrivateKey);
        }
    }

    private static string? Carried(OpenIddictServerEvents.GenerateTokenContext context) =>
        context.TokenType switch
        {
            OpenIddictConstants.TokenTypeIdentifiers.Private.AuthorizationCode =>
                context.Principal?.GetClaim(OidcClaimNames.Code),
            OpenIddictConstants.TokenTypeIdentifiers.RefreshToken =>
                context.Principal?.GetClaim(OidcClaimNames.RefreshToken),
            _ => null,
        };
}
