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
/// Google and Apple as the deployment reaches them: each publishes a document naming
/// its issuer and its key set, signs its security events with a key generated here, and
/// exchanges a code it issued for an identity token it signs with the same key.
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

    /// <summary>
    /// Where Google sends a person to sign in.
    /// </summary>
    public const string GoogleAuthorization = "https://accounts.google.test/o/oauth2/v2/auth";

    /// <summary>
    /// Where Apple sends a person to sign in.
    /// </summary>
    public const string AppleAuthorization = "https://appleid.apple.test/auth/authorize";

    private const string GoogleIssuer = "https://accounts.google.test/";

    private const string AppleIssuer = "https://appleid.apple.test";

    private const string GoogleKey = "google-key-1";

    private const string AppleKey = "apple-key-1";

    private static readonly Uri GoogleMetadata = new("https://accounts.google.test/.well-known/risc-configuration");

    private static readonly Uri GoogleConfiguration = new("https://accounts.google.test/.well-known/openid-configuration");

    private static readonly Uri GoogleKeys = new("https://www.googleapis.test/oauth2/v3/certs");

    private static readonly Uri GoogleToken = new("https://oauth2.googleapis.test/token");

    private static readonly Uri AppleMetadata = new("https://appleid.apple.test/.well-known/openid-configuration");

    private static readonly Uri AppleKeys = new("https://appleid.apple.test/auth/keys");

    private static readonly Uri AppleToken = new("https://appleid.apple.test/auth/token");

    private static readonly Uri GoogleReturn = new("https://identity.example.test/callbacks/providers/google/return");

    private static readonly Uri AppleReturn = new("https://identity.example.test/callbacks/providers/apple/return");

    private readonly RSA _google = RSA.Create(2048);

    private readonly RSA _apple = RSA.Create(2048);

    private readonly RSA _stranger = RSA.Create(2048);

    private readonly Dictionary<string, Issued> _issued = new(StringComparer.Ordinal);

    private readonly List<IReadOnlyDictionary<string, string>> _exchanges = [];

    /// <summary>
    /// What the deployment declares for Google.
    /// </summary>
    public SocialProvider Google { get; } = new(
        Factor.Google,
        GoogleMetadata,
        [GoogleClient],
        GoogleConfiguration,
        GoogleReturn,
        Encoding.UTF8.GetBytes("the-google-client-secret"));

    /// <summary>
    /// What the deployment declares for Apple, whose discovery document is also the one
    /// its events are verified against.
    /// </summary>
    public SocialProvider Apple { get; } = new(
        Factor.Apple,
        AppleMetadata,
        [AppleClient],
        AppleMetadata,
        AppleReturn,
        Encoding.UTF8.GetBytes("the-apple-signed-secret"));

    /// <summary>
    /// Whether the providers' documents can be read at all.
    /// </summary>
    public bool Reachable { get; set; } = true;

    /// <summary>
    /// What each exchange the deployment made presented, in order.
    /// </summary>
    public IReadOnlyList<IReadOnlyDictionary<string, string>> Exchanges => _exchanges;

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

    /// <summary>
    /// Issues a code, as the provider does once the person has signed in there, for the
    /// identity token the exchange of it answers with.
    /// </summary>
    /// <param name="provider">Which provider.</param>
    /// <param name="authorization">Where the deployment sent the browser.</param>
    /// <param name="person">Who signed in.</param>
    /// <returns>The code.</returns>
    public string Issue(Factor provider, string authorization, ProviderPerson person)
    {
        ArgumentNullException.ThrowIfNull(authorization);
        ArgumentNullException.ThrowIfNull(person);

        string code = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(24));

        _issued[code] = new Issued(
            provider,
            person,
            Parameter(authorization, "nonce"),
            Parameter(authorization, "code_challenge"));

        return code;
    }

    /// <summary>
    /// One parameter of an address, unescaped, or nothing where it carries none.
    /// </summary>
    /// <param name="address">The address.</param>
    /// <param name="name">Which parameter.</param>
    /// <returns>Its value.</returns>
    public static string? Parameter(string address, string name)
    {
        ArgumentNullException.ThrowIfNull(address);

        int at = address.IndexOf('?', StringComparison.Ordinal);

        if (at < 0)
        {
            return null;
        }

        foreach (string pair in address[(at + 1)..].Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            int equals = pair.IndexOf('=', StringComparison.Ordinal);

            if (string.Equals(pair[..equals], name, StringComparison.Ordinal))
            {
                return Uri.UnescapeDataString(pair[(equals + 1)..]);
            }
        }

        return null;
    }

    /// <inheritdoc/>
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (Reachable && request.Method == HttpMethod.Post
            && (request.RequestUri == GoogleToken || request.RequestUri == AppleToken))
        {
            return await ExchangedAsync(request, cancellationToken);
        }

        string? document = !Reachable ? null : request.RequestUri switch
        {
            Uri asked when asked == GoogleMetadata => Metadata(GoogleIssuer, GoogleKeys),
            Uri asked when asked == GoogleConfiguration => Configuration(
                GoogleIssuer,
                GoogleKeys,
                GoogleAuthorization,
                GoogleToken,
                proofKey: true),
            Uri asked when asked == GoogleKeys => KeySet(_google, GoogleKey),
            Uri asked when asked == AppleMetadata => Configuration(
                AppleIssuer,
                AppleKeys,
                AppleAuthorization,
                AppleToken,
                proofKey: false),
            Uri asked when asked == AppleKeys => KeySet(_apple, AppleKey),
            _ => null,
        };

        return document is null
            ? new HttpResponseMessage(HttpStatusCode.NotFound)
            : new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(document, Encoding.UTF8, "application/json"),
            };
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

    // Google lists the proof key it takes; Apple lists none, so no proof key is sent
    // it.
    private static string Configuration(
        string issuer,
        Uri keys,
        string authorization,
        Uri token,
        bool proofKey)
    {
        var document = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["issuer"] = issuer,
            ["jwks_uri"] = keys.AbsoluteUri,
            ["authorization_endpoint"] = authorization,
            ["token_endpoint"] = token.AbsoluteUri,
            ["response_modes_supported"] = new[] { "query", "fragment", "form_post" },
        };

        if (proofKey)
        {
            document["code_challenge_methods_supported"] = new[] { "plain", "S256" };
        }

        return JsonSerializer.Serialize(document);
    }

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

    private static string Challenge(string verifier) =>
        Base64Url.EncodeToString(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));

    // The provider redeems a code once, for the client it was issued to, presenting the
    // secret it holds for that client, the return it was issued for and the proof key
    // it was challenged with.
    private async Task<HttpResponseMessage> ExchangedAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var presented = new Dictionary<string, string>(StringComparer.Ordinal);
        string form = await request.Content!.ReadAsStringAsync(cancellationToken);

        foreach (string pair in form.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            int equals = pair.IndexOf('=', StringComparison.Ordinal);

            presented[Uri.UnescapeDataString(pair[..equals].Replace('+', ' '))] =
                Uri.UnescapeDataString(pair[(equals + 1)..].Replace('+', ' '));
        }

        _exchanges.Add(presented);

        bool google = request.RequestUri == GoogleToken;
        SocialProvider declared = google ? Google : Apple;

        if (!presented.TryGetValue("code", out string? code)
            || !_issued.Remove(code, out Issued? issued)
            || issued.Provider != declared.Provider
            || presented.GetValueOrDefault("grant_type") is not "authorization_code"
            || presented.GetValueOrDefault("client_id") != declared.ClientIds[0]
            || presented.GetValueOrDefault("client_secret") != Encoding.UTF8.GetString(declared.Secret.Span)
            || presented.GetValueOrDefault("redirect_uri") != declared.Return.AbsoluteUri
            || (issued.Challenge is string challenge
                && (!presented.TryGetValue("code_verifier", out string? verifier)
                    || Challenge(verifier) != challenge)))
        {
            return new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent("{\"error\":\"invalid_grant\"}", Encoding.UTF8, "application/json"),
            };
        }

        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["access_token"] = "an-access-token-the-deployment-never-uses",
                    ["token_type"] = "Bearer",
                    ["id_token"] = Identity(issued, google),
                }),
                Encoding.UTF8,
                "application/json"),
        };
    }

    private string Identity(Issued issued, bool google)
    {
        ProviderPerson person = issued.Person;
        var claims = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["sub"] = person.Subject,
            ["iat"] = 1772366400,
            ["exp"] = person.Expired ? 946684800L : 4102444800L,
        };

        if ((person.Nonce ?? issued.Nonce) is string nonce)
        {
            claims["nonce"] = nonce;
        }

        if (person.Email is string email)
        {
            claims["email"] = email;
        }

        if (person.Verified is object verified)
        {
            claims["email_verified"] = verified;
        }

        if (person.HostedDomain is string hostedDomain)
        {
            claims["hd"] = hostedDomain;
        }

        return new JsonWebTokenHandler { SetDefaultTimesOnTokenCreation = false }.CreateToken(
            new SecurityTokenDescriptor
            {
                Issuer = google ? GoogleIssuer : AppleIssuer,
                Audience = person.Audience ?? (google ? GoogleClient : AppleClient),
                Claims = claims,
                SigningCredentials = new SigningCredentials(
                    new RsaSecurityKey(person.Forged ? _stranger : google ? _google : _apple)
                    {
                        KeyId = google ? GoogleKey : AppleKey,
                    },
                    SecurityAlgorithms.RsaSha256),
            });
    }

    private sealed record Issued(Factor Provider, ProviderPerson Person, string? Nonce, string? Challenge);
}
