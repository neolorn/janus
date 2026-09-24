using System;
using System.Buffers.Text;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Janus.Hosting.Tests.Oidc;

/// <summary>
/// What the mail server's adapter does with a token it is handed: it verifies the
/// signature against the set the provider publishes, the type, the issuer, the
/// audience and the lifetime, offline, and believes nothing else (AUTH-OIDC-006 AC3,
/// INT-MAIL-004).
/// </summary>
internal static class MailServerAdapter
{
    /// <summary>
    /// The claims RFC 9068 has every access token carry.
    /// </summary>
    public static readonly string[] Required = ["aud", "client_id", "exp", "iat", "iss", "jti", "sub"];

    /// <summary>
    /// Verifies a token as the adapter configured for one client of the provider does.
    /// </summary>
    /// <param name="deployment">The provider whose published set and issuer are read.</param>
    /// <param name="token">The token presented.</param>
    /// <param name="audience">The client the adapter is configured as.</param>
    /// <returns>What the verification made of it.</returns>
    public static async Task<TokenValidationResult> VerifyAsync(
        Deployment deployment,
        string token,
        string audience)
    {
        var machine = new Machine(deployment);
        string issuer = (await machine.GetAsync("/.well-known/openid-configuration", bearer: string.Empty))
            .Text("issuer");
        var published = new JsonWebKeySet(
            (await machine.GetAsync("/oidc/jwks", bearer: string.Empty)).Body);

        return await new JsonWebTokenHandler().ValidateTokenAsync(
            token,
            new TokenValidationParameters
            {
                IssuerSigningKeys = published.GetSigningKeys(),
                ValidIssuer = issuer,
                ValidAudience = audience,
                ValidTypes = ["at+jwt"],
                LifetimeValidator = (before, expires, _, _) =>
                    (before is null || before <= deployment.Clock.GetUtcNow().UtcDateTime)
                    && (expires is null || expires > deployment.Clock.GetUtcNow().UtcDateTime),
            });
    }

    /// <summary>
    /// The header of a token, as JSON.
    /// </summary>
    /// <param name="token">The token.</param>
    /// <returns>The header.</returns>
    public static JsonElement Header(string token) => Part(token, 0);

    /// <summary>
    /// The claims of a token, as JSON.
    /// </summary>
    /// <param name="token">The token.</param>
    /// <returns>The claims.</returns>
    public static JsonElement Claims(string token) => Part(token, 1);

    /// <summary>
    /// The names of the claims a token carries, ordered.
    /// </summary>
    /// <param name="token">The token.</param>
    /// <returns>The names.</returns>
    public static string[] Named(string token) =>
        [.. Claims(token).EnumerateObject().Select(claim => claim.Name).Order(StringComparer.Ordinal)];

    private static JsonElement Part(string token, int index) =>
        JsonDocument.Parse(Base64Url.DecodeFromChars(token.Split('.')[index])).RootElement;
}
