using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication;
using Janus.Authentication.Oidc;
using Janus.Core;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Abstractions;
using OpenIddict.Server;

namespace Janus.Hosting.Oidc;

/// <summary>
/// What the server makes of a code or a refresh token the library issued.
/// </summary>
/// <param name="codes">Where a code is held.</param>
/// <param name="tokens">Where a refresh token is held.</param>
/// <param name="oidc">Where the published keys are read.</param>
/// <remarks>
/// Implements AUTH-SESS-012 and AUTH-OIDC-003. Neither value carries anything the
/// server could read, so what the row says is handed to it: the client it was issued
/// to, the destination and the challenge it was issued against, and when it stops
/// being exchangeable. Whether it is still unspent is settled where the exchange is,
/// against the same row, in the transaction that spends it.
/// <para>
/// An access token is the server's own signed one and is validated as such, against
/// the keys the deployment publishes rather than the key the server holds (AUTH-KEY-001).
/// </para>
/// </remarks>
internal sealed class TokenRecognition(
    IAuthorizationCodeStore codes,
    IRefreshTokenStore tokens,
    IOidc oidc)
    : IOpenIddictServerHandler<OpenIddictServerEvents.ValidateTokenContext>
{
    /// <summary>
    /// Where the handler sits: after the server has settled what it would validate a
    /// token against, and before it tries to read the value itself.
    /// </summary>
    public const int Order = int.MinValue + 102_500;

    /// <inheritdoc/>
    public async ValueTask HandleAsync(OpenIddictServerEvents.ValidateTokenContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        string? kind = context.ValidTokenTypes.Count is 1 ? context.ValidTokenTypes.Single() : null;

        ClaimsPrincipal? held = kind switch
        {
            OpenIddictConstants.TokenTypeIdentifiers.Private.AuthorizationCode =>
                await CodeAsync(context.Token, context.CancellationToken).ConfigureAwait(false),
            OpenIddictConstants.TokenTypeIdentifiers.RefreshToken =>
                await RefreshAsync(context.Token, context.CancellationToken).ConfigureAwait(false),
            _ => null,
        };

        if (held is not null)
        {
            context.Principal = held.SetTokenType(kind);

            return;
        }

        await PublishedAsync(context.TokenValidationParameters, context.CancellationToken)
            .ConfigureAwait(false);
    }

    private static IReadOnlyList<PublishedSigningKey> Withheld(Error error, ref Error? failure)
    {
        failure = error;

        return default!;
    }

    private static ClaimsPrincipal Bare() =>
        new(new ClaimsIdentity(
            OpenIddictConstants.TokenTypeIdentifiers.Private.AuthorizationCode,
            OpenIddictConstants.Claims.Name,
            OpenIddictConstants.Claims.Role));

    // What the server signed it with is the key the deployment held at the time, so
    // what validates it is the published set and not the server's own key.
    private async ValueTask PublishedAsync(
        TokenValidationParameters parameters,
        CancellationToken cancellationToken)
    {
        Error? failure = null;

        IReadOnlyList<PublishedSigningKey> published = (await oidc
                .KeysAsync(cancellationToken)
                .ConfigureAwait(false))
            .Match(keys => keys, error => Withheld(error, ref failure));

        if (failure is null)
        {
            JsonWebKey[] set = [.. published.Select(PublishedKeys.Of)];

            parameters.IssuerSigningKeys = set;
            parameters.IssuerSigningKeyResolver = (_, _, _, _) => set;
        }
    }

    private async ValueTask<ClaimsPrincipal?> CodeAsync(
        string token,
        CancellationToken cancellationToken)
    {
        if (await codes
                .FindAsync(OpaqueToken.Of(token).Fingerprint(), cancellationToken)
                .ConfigureAwait(false) is not AuthorizationCode code)
        {
            return null;
        }

        return Bare()
            .SetPresenters(code.ClientId)
            .SetCreationDate(code.IssuedAt)
            .SetExpirationDate(code.ExpiresAt)
            .SetScopes(code.Scope.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .SetClaim(OpenIddictConstants.Claims.Subject, code.Subject.ToString())
            .SetClaim(OpenIddictConstants.Claims.Private.RedirectUri, code.Redirect)
            .SetClaim(OpenIddictConstants.Claims.Private.CodeChallenge, code.Challenge)
            .SetClaim(OpenIddictConstants.Claims.Private.CodeChallengeMethod, code.ChallengeMethod);
    }

    private async ValueTask<ClaimsPrincipal?> RefreshAsync(
        string token,
        CancellationToken cancellationToken)
    {
        if (await tokens
                .FindAsync(OpaqueToken.Of(token).Fingerprint(), cancellationToken)
                .ConfigureAwait(false) is not RefreshToken held)
        {
            return null;
        }

        return Bare()
            .SetPresenters(held.ClientId)
            .SetCreationDate(held.IssuedAt)
            .SetExpirationDate(held.ExpiresAt)
            .SetScopes(held.Scope.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .SetClaim(OpenIddictConstants.Claims.Subject, held.Subject.ToString());
    }
}
