using System;
using System.Linq;
using System.Threading.Tasks;
using Janus.Authentication.Oidc;
using Janus.Core;
using Janus.Core.Configuration;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Abstractions;
using OpenIddict.Server;

namespace Janus.Hosting.Oidc;

/// <summary>
/// What the server validates a token it issued against: for an access token, the keys the
/// key set publishes; for a code, a refresh token or any other token of its own, every key
/// in the set, a retired key's kept public key included.
/// </summary>
/// <param name="source">The signing keys.</param>
/// <param name="configuration">Where the cadence is read.</param>
/// <param name="time">The clock the deployment runs on.</param>
/// <remarks>
/// Implements AUTH-KEY-001 AC2, AC3 and AC6 and CONV-CODE-007. It runs after the server
/// has copied its validation parameters from the options and before it reads the token,
/// and sets the keys on that copy, so the signing keys the options took when they were
/// built are never read (D-181). A refresh token lives as long as its session, so the
/// provider's own tokens are checked against the retired keys it keeps; an access token is
/// accepted only under a key a relying party could still validate it with.
/// </remarks>
internal sealed class TokenValidationKeys(
    SigningCredentialSource source,
    IConfigurationStore configuration,
    TimeProvider time)
    : IOpenIddictServerHandler<OpenIddictServerEvents.ValidateTokenContext>
{
    /// <summary>
    /// Where the handler sits: after the server has settled what it would validate a
    /// token against, and before it reads the value itself.
    /// </summary>
    public const int Order = int.MinValue + 102_500;

    // The type an access token's signed header names, bare and as a media type.
    private const string AccessTokenType = OpenIddictConstants.JsonWebTokenTypes.AccessToken;

    private const string AccessTokenMediaType =
        OpenIddictConstants.JsonWebTokenTypes.Prefixes.Application + OpenIddictConstants.JsonWebTokenTypes.AccessToken;

    /// <inheritdoc/>
    public async ValueTask HandleAsync(OpenIddictServerEvents.ValidateTokenContext context)
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

        JsonWebKey[] published = [.. set.Published(time.GetUtcNow()).Select(held => held.PublicKey)];
        JsonWebKey[] kept = [.. set.Keys.Select(held => held.PublicKey)];
        bool accessTokensOnly = context.ValidTokenTypes.Count == 1
            && context.ValidTokenTypes.Contains(OpenIddictConstants.TokenTypeIdentifiers.AccessToken);

        context.TokenValidationParameters.TryAllIssuerSigningKeys = false;
        context.TokenValidationParameters.IssuerSigningKeys = accessTokensOnly ? published : kept;
        context.TokenValidationParameters.IssuerSigningKeyResolver = (_, token, _, _) =>
            accessTokensOnly || IsAccessToken(token) ? published : kept;
    }

    private static SigningKeySet Withheld(Error error, ref Error? failure)
    {
        failure = error;

        return default!;
    }

    // An access token names its type in its signed header, as the server wrote it; a
    // media type is compared without regard to case.
    private static bool IsAccessToken(SecurityToken token) =>
        token is JsonWebToken { Typ: string type }
        && (string.Equals(type, AccessTokenType, StringComparison.OrdinalIgnoreCase)
            || string.Equals(type, AccessTokenMediaType, StringComparison.OrdinalIgnoreCase));
}
