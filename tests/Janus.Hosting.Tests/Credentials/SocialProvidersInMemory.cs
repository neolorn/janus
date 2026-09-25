using System;
using System.Buffers.Text;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Janus.Hosting.Tests.Credentials;

/// <summary>
/// Google and Apple as the deployment reaches them for their keys: each publishes a
/// document naming its issuer and its key set, and signs its security events with a key
/// generated here.
/// </summary>
internal sealed class SocialProvidersInMemory : HttpMessageHandler
{
    /// <summary>
    /// The deployment's client at Google.
    /// </summary>
    public const string GoogleClient = "the-client.apps.google.test";

    /// <summary>
    /// The deployment's client at Apple.
    /// </summary>
    public const string AppleClient = "test.example.identity";

    private const string GoogleIssuer = "https://accounts.google.test/";

    private const string AppleIssuer = "https://appleid.apple.test";

    private const string GoogleKey = "google-key-1";

    private const string AppleKey = "apple-key-1";

    private static readonly Uri GoogleMetadata = new("https://accounts.google.test/.well-known/risc-configuration");

    private static readonly Uri GoogleKeys = new("https://www.googleapis.test/oauth2/v3/certs");

    private static readonly Uri AppleMetadata = new("https://appleid.apple.test/.well-known/openid-configuration");

    private static readonly Uri AppleKeys = new("https://appleid.apple.test/auth/keys");

    private readonly RSA _google = RSA.Create(2048);

    private readonly RSA _apple = RSA.Create(2048);

    private readonly RSA _stranger = RSA.Create(2048);

    /// <summary>
    /// What the deployment declares for Google.
    /// </summary>
    public SocialProvider Google { get; } = new(Factor.Google, GoogleMetadata, [GoogleClient]);

    /// <summary>
    /// What the deployment declares for Apple.
    /// </summary>
    public SocialProvider Apple { get; } = new(Factor.Apple, AppleMetadata, [AppleClient]);

    /// <summary>
    /// Whether the providers' documents can be read at all.
    /// </summary>
    public bool Reachable { get; set; } = true;

    /// <summary>
    /// An event the provider signed, addressed to the deployment's client there.
    /// </summary>
    /// <param name="provider">Which provider.</param>
    /// <param name="eventId">The event's identifier.</param>
    /// <param name="events">The event claim, as the provider writes it.</param>
    /// <returns>The token.</returns>
    public string Signed(Factor provider, string eventId, object events) =>
        Token(provider, eventId, events, provider is Factor.Google ? _google : _apple);

    /// <summary>
    /// An event that names the provider's key and was signed with another.
    /// </summary>
    /// <param name="provider">Which provider it claims to come from.</param>
    /// <param name="eventId">The event's identifier.</param>
    /// <param name="events">The event claim, as the provider writes it.</param>
    /// <returns>The token.</returns>
    public string Forged(Factor provider, string eventId, object events) =>
        Token(provider, eventId, events, _stranger);

    /// <inheritdoc/>
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        string? document = !Reachable ? null : request.RequestUri switch
        {
            Uri asked when asked == GoogleMetadata => Metadata(GoogleIssuer, GoogleKeys),
            Uri asked when asked == GoogleKeys => KeySet(_google, GoogleKey),
            Uri asked when asked == AppleMetadata => Metadata(AppleIssuer, AppleKeys),
            Uri asked when asked == AppleKeys => KeySet(_apple, AppleKey),
            _ => null,
        };

        return Task.FromResult(
            document is null
                ? new HttpResponseMessage(HttpStatusCode.NotFound)
                : new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(document, Encoding.UTF8, "application/json"),
                });
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _google.Dispose();
            _apple.Dispose();
            _stranger.Dispose();
        }

        base.Dispose(disposing);
    }

    private static string Metadata(string issuer, Uri keys) =>
        JsonSerializer.Serialize(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["issuer"] = issuer,
            ["jwks_uri"] = keys.AbsoluteUri,
        });

    private static string KeySet(RSA key, string keyId)
    {
        RSAParameters published = key.ExportParameters(includePrivateParameters: false);

        return JsonSerializer.Serialize(new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["keys"] = new[]
            {
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["kty"] = "RSA",
                    ["use"] = "sig",
                    ["alg"] = "RS256",
                    ["kid"] = keyId,
                    ["n"] = Base64Url.EncodeToString(published.Modulus),
                    ["e"] = Base64Url.EncodeToString(published.Exponent),
                },
            },
        });
    }

    // A security event states no lifetime, so the token carries none.
    private static string Token(Factor provider, string eventId, object events, RSA signer)
    {
        bool google = provider is Factor.Google;

        return new JsonWebTokenHandler { SetDefaultTimesOnTokenCreation = false }.CreateToken(
            new SecurityTokenDescriptor
            {
                Issuer = google ? GoogleIssuer : AppleIssuer,
                Audience = google ? GoogleClient : AppleClient,
                Claims = new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    ["jti"] = eventId,
                    ["iat"] = 1772366400,
                    ["events"] = events,
                },
                SigningCredentials = new SigningCredentials(
                    new RsaSecurityKey(signer) { KeyId = google ? GoogleKey : AppleKey },
                    SecurityAlgorithms.RsaSha256),
            });
    }
}
