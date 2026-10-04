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
/// exchanges a code it issued for an identity token it signs with the same key. Google
/// takes the static secret it issued; Apple takes a client secret signed with the key it
/// issued the deployment, and holds the public half to verify it.
/// </summary>
/// <param name="clock">The deployment's clock, which a client secret's lifetime is judged by.</param>
internal sealed class SocialProvidersInMemory(TimeProvider clock) : HttpMessageHandler
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

    private const string GoogleSecret = "the-google-client-secret";

    private const string GoogleKey = "google-key-1";

    private const string AppleKey = "apple-key-1";

    /// <summary>
    /// The identifier Apple knows the deployment's account by.
    /// </summary>
    public const string AppleTeam = "TEAMID0001";

    /// <summary>
    /// The identifier of the key Apple issued the deployment for its client secret.
    /// </summary>
    public const string AppleSecretKey = "SECRETKEY1";

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

    private readonly ECDsa _appleSecret = ECDsa.Create(ECCurve.NamedCurves.nistP256);

    private readonly Dictionary<string, Issued> _issued = new(StringComparer.Ordinal);

    private readonly List<IReadOnlyDictionary<string, string>> _exchanges = [];

    private int _calls;

    /// <summary>
    /// What the deployment declares for Google.
    /// </summary>
    public SocialProvider Google { get; } = new(
        Factor.Google,
        GoogleMetadata,
        [GoogleClient],
        GoogleConfiguration,
        GoogleReturn);

    /// <summary>
    /// What the deployment declares for Apple, whose discovery document is also the one
    /// its events are verified against.
    /// </summary>
    public SocialProvider Apple { get; } = new(
        Factor.Apple,
        AppleMetadata,
        [AppleClient],
        AppleMetadata,
        AppleReturn);

    /// <summary>
    /// What the host's secret source answers for each provider, by its name: the
    /// static secret Google issued, and the signing credential Apple issued.
    /// </summary>
    public IReadOnlyDictionary<string, ProviderCredential> Credentials =>
        new Dictionary<string, ProviderCredential>(StringComparer.Ordinal)
        {
            ["google"] = ProviderCredential.Secret(Encoding.UTF8.GetBytes(GoogleSecret)),
            ["apple"] = ProviderCredential.Signed(AppleTeam, AppleSecretKey, _appleSecret.ExportPkcs8PrivateKey()),
        };

    /// <summary>
    /// The public half of the key Apple issued the deployment, which a client secret
    /// presented to it verifies under.
    /// </summary>
    public SecurityKey AppleSecretVerifier => new ECDsaSecurityKey(_appleSecret) { KeyId = AppleSecretKey };

    /// <summary>
    /// Whether the providers' documents can be read at all.
    /// </summary>
    public bool Reachable { get; set; } = true;

    /// <summary>
    /// What each exchange the deployment made presented, in order.
    /// </summary>
    public IReadOnlyList<IReadOnlyDictionary<string, string>> Exchanges => _exchanges;

    /// <summary>
    /// How many requests the deployment has made of either provider, of any kind.
    /// </summary>
    public int Calls => Volatile.Read(ref _calls);

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
    /// An event the provider signed that departs from what it sends in the ways named:
    /// one carrying no identifier, issued by another issuer, addressed to another
    /// client, naming a key the provider does not publish, or stating a lifetime.
    /// </summary>
    /// <param name="provider">Which provider.</param>
    /// <param name="eventId">The event's identifier, or nothing for a token carrying none.</param>
    /// <param name="events">The event claim, as the provider writes it.</param>
    /// <param name="issuer">Who it says issued it, where not the provider.</param>
    /// <param name="audience">Whom it is addressed to, where not the deployment's client.</param>
    /// <param name="keyId">The key it names, where not the provider's.</param>
    /// <param name="expires">When it says it expires, in seconds of the epoch, where it says so.</param>
    /// <returns>The token.</returns>
    public string Departing(
        Factor provider,
        string? eventId,
        object events,
        string? issuer = null,
        string? audience = null,
        string? keyId = null,
        long? expires = null) =>
        Token(provider, eventId, events, provider is Factor.Google ? _google : _apple, issuer, audience, keyId, expires);

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

        _ = Interlocked.Increment(ref _calls);

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
            _appleSecret.Dispose();
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

    // A security event states no lifetime, so the token carries none unless it is told
    // to.
    private static string Token(
        Factor provider,
        string? eventId,
        object events,
        RSA signer,
        string? issuer = null,
        string? audience = null,
        string? keyId = null,
        long? expires = null)
    {
        bool google = provider is Factor.Google;
        var claims = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["iat"] = 1772366400,
            ["events"] = events,
        };

        if (eventId is not null)
        {
            claims["jti"] = eventId;
        }

        if (expires is long lapses)
        {
            claims["exp"] = lapses;
        }

        return new JsonWebTokenHandler { SetDefaultTimesOnTokenCreation = false }.CreateToken(
            new SecurityTokenDescriptor
            {
                Issuer = issuer ?? (google ? GoogleIssuer : AppleIssuer),
                Audience = audience ?? (google ? GoogleClient : AppleClient),
                Claims = claims,
                SigningCredentials = new SigningCredentials(
                    new RsaSecurityKey(signer) { KeyId = keyId ?? (google ? GoogleKey : AppleKey) },
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
        bool secret = google
            ? presented.GetValueOrDefault("client_secret") == GoogleSecret
            : await MintedAsync(presented.GetValueOrDefault("client_secret"));

        if (!presented.TryGetValue("code", out string? code)
            || !_issued.Remove(code, out Issued? issued)
            || issued.Provider != declared.Provider
            || presented.GetValueOrDefault("grant_type") is not "authorization_code"
            || presented.GetValueOrDefault("client_id") != declared.ClientIds[0]
            || !secret
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

    // What Apple checks of a client secret it is presented: signed with ES256 under the
    // key it issued, by the account it issued it to, for the client the code was issued
    // to and for itself, made to last five minutes, and not lapsed by the deployment's
    // clock.
    private async Task<bool> MintedAsync(string? secret)
    {
        TokenValidationResult read = await new JsonWebTokenHandler()
            .ValidateTokenAsync(
                secret,
                new TokenValidationParameters
                {
                    IssuerSigningKey = AppleSecretVerifier,
                    ValidAlgorithms = [SecurityAlgorithms.EcdsaSha256],
                    ValidIssuer = AppleTeam,
                    ValidAudience = AppleIssuer,
                    LifetimeValidator = (_, expires, _, _) => expires > clock.GetUtcNow().UtcDateTime,
                });

        return read.IsValid
            && read.SecurityToken is JsonWebToken minted
            && minted.Kid == AppleSecretKey
            && minted.Subject == AppleClient
            && minted.ValidTo - minted.IssuedAt == TimeSpan.FromMinutes(5);
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
