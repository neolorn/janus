using System;
using System.Buffers.Text;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Oidc;
using Janus.Core;

namespace Janus.Hosting.Bff;

/// <summary>
/// The provider's refusals, asked of the deployment over its own endpoints by this
/// application's half of the sign-on, as its own client.
/// </summary>
/// <param name="client">Which client of the provider this application is.</param>
/// <param name="secrets">What it authenticates itself with at the provider, read from the registry.</param>
/// <param name="addresses">Where the provider answers.</param>
/// <param name="clients">Where the client's one destination is registered.</param>
/// <param name="channel">Where the back-channel requests are made from.</param>
/// <param name="randomness">Where the verifier and each state are drawn from.</param>
/// <remarks>
/// Implements LIB-TEST-001 AC4 and AC5, AUTH-OIDC-006 AC1 and BFF-SESS-006 (D-172). Each
/// form the two specifications retire is asked for with everything else in order, so
/// what is refused is the form. The endpoints are the ones the discovery document names,
/// so no route of the deployment's is assumed (LIB-HOST-003). The client's secret is read
/// from the registry for each request that presents it and dropped with the request, as
/// the sign-on reads it (OPS-SEC-002), and no finding carries it.
/// </remarks>
internal sealed class ProviderProbes(
    SignOnClient client,
    RegisteredSecrets secrets,
    AuthenticationAddresses addresses,
    IOidcClientStore clients,
    IHttpClientFactory channel,
    RandomNumberGenerator randomness) : IProviderProbes
{
    private const string UnsupportedResponseType = "unsupported_response_type";

    private const string UnsupportedGrantType = "unsupported_grant_type";

    private const string InvalidRequest = "invalid_request";

    private const string InvalidClient = "invalid_client";

    private const string Secret = "client_secret";

    // RFC 2606: a name that resolves nowhere, so no deployment registers it.
    private const string Elsewhere = "https://unregistered.invalid/";

    // The implicit form and every hybrid one, each of which hands a token to the
    // browser.
    private static readonly string[] Implicit =
    [
        "token",
        "id_token",
        "id_token token",
        "code id_token",
        "code token",
        "code id_token token",
    ];

    // The password grant and every grant beside the code and the refresh.
    private static readonly string[] Retired =
    [
        "password",
        "client_credentials",
        "implicit",
        "urn:ietf:params:oauth:grant-type:device_code",
        "urn:ietf:params:oauth:grant-type:token-exchange",
    ];

    /// <inheritdoc/>
    public async ValueTask<Result<IReadOnlyList<Error>>> RunAsync(
        AccessContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (await clients.FindAsync(client.ClientId, cancellationToken).ConfigureAwait(false)
            is not OidcClient registered)
        {
            return Result.Failure<IReadOnlyList<Error>>(Error.From(ErrorCodes.SystemFault));
        }

        using HttpClient requests = channel.CreateClient(SignOn.Channel);

        var findings = new List<Error>();
        var discovery = new Uri(addresses.Provider.TrimEnd('/') + "/.well-known/openid-configuration");

        Answer described = await SentAsync(requests, HttpMethod.Get, discovery, fields: null, cancellationToken)
            .ConfigureAwait(false);

        if (described.Document is not JsonElement document
            || Endpoint(document, "pushed_authorization_request_endpoint") is not Uri push
            || Endpoint(document, "token_endpoint") is not Uri token)
        {
            findings.Add(Finding(new Probe("discovery", discovery, "document", [], "served", Authenticated: false), described));

            return Result.Success<IReadOnlyList<Error>>(findings);
        }

        findings.AddRange(Advertised(document));

        foreach (Probe probe in Probes(registered, push, token))
        {
            Error? failure = null;

            Answer answered = (await AnsweredAsync(requests, registered, probe, cancellationToken).ConfigureAwait(false))
                .Match(answer => answer, error => Withheld<Answer>(error, ref failure));

            if (failure is not null)
            {
                return Result.Failure<IReadOnlyList<Error>>(failure);
            }

            // A refusal that still hands back a reference has issued what it refused.
            if (!string.Equals(answered.Error, probe.Expected, StringComparison.Ordinal)
                || answered.RequestUri is not null)
            {
                findings.Add(Finding(probe, answered));
            }
        }

        return Result.Success<IReadOnlyList<Error>>(findings);
    }

    private static TValue Withheld<TValue>(Error error, ref Error? failure)
    {
        failure = error;

        return default!;
    }

    private static Uri? Endpoint(JsonElement document, string member) =>
        document.ValueKind == JsonValueKind.Object
        && document.TryGetProperty(member, out JsonElement named)
        && named.ValueKind == JsonValueKind.String
        && Uri.TryCreate(named.GetString(), UriKind.Absolute, out Uri? endpoint)
            ? endpoint
            : null;

    private static IReadOnlyList<string>? Listed(JsonElement document, string member) =>
        document.TryGetProperty(member, out JsonElement listed) && listed.ValueKind == JsonValueKind.Array
            ? [.. listed.EnumerateArray().Select(value => value.GetString() ?? string.Empty)]
            : null;

    // AUTH-OIDC-006 AC1 and AC4: the document names the code flow, its proof key by
    // S256, the code and refresh grants, the pushed request as required, and no way
    // for a client to go unauthenticated.
    private static IEnumerable<Error> Advertised(JsonElement document)
    {
        if (Listed(document, "response_types_supported") is not ["code"])
        {
            yield return Listing(document, "response_types_supported");
        }

        if (Listed(document, "code_challenge_methods_supported") is not ["S256"])
        {
            yield return Listing(document, "code_challenge_methods_supported");
        }

        if (Listed(document, "grant_types_supported") is not IReadOnlyList<string> grants
            || !grants.Order(StringComparer.Ordinal).SequenceEqual(["authorization_code", "refresh_token"]))
        {
            yield return Listing(document, "grant_types_supported");
        }

        if (!document.TryGetProperty("require_pushed_authorization_requests", out JsonElement required)
            || required.ValueKind != JsonValueKind.True)
        {
            yield return Listing(document, "require_pushed_authorization_requests");
        }

        if (Listed(document, "token_endpoint_auth_methods_supported") is IReadOnlyList<string> methods
            && methods.Contains("none", StringComparer.Ordinal))
        {
            yield return Listing(document, "token_endpoint_auth_methods_supported");
        }
    }

    private static Error Listing(JsonElement document, string member) =>
        new(
            ErrorCodes.ProviderNonconformant,
            new Dictionary<string, JsonElement>(StringComparer.Ordinal)
            {
                ["probe"] = JsonSerializer.SerializeToElement("discovery"),
                ["member"] = JsonSerializer.SerializeToElement(member),
                ["listed"] = document.TryGetProperty(member, out JsonElement listed)
                    ? listed.Clone()
                    : JsonSerializer.SerializeToElement<string?>(null),
            });

    // What was sent is read from the probe's own fields, which never hold the secret:
    // it is added to a request only as the request is made.
    private static Error Finding(Probe probe, Answer answered) =>
        new(
            ErrorCodes.ProviderNonconformant,
            new Dictionary<string, JsonElement>(StringComparer.Ordinal)
            {
                ["probe"] = JsonSerializer.SerializeToElement(probe.Name),
                ["field"] = JsonSerializer.SerializeToElement(probe.Field),
                ["sent"] = JsonSerializer.SerializeToElement(probe.Fields
                    .FirstOrDefault(field => string.Equals(field.Name, probe.Field, StringComparison.Ordinal))
                    .Value),
                ["expected"] = JsonSerializer.SerializeToElement(probe.Expected),
                ["status"] = JsonSerializer.SerializeToElement(answered.Status),
                ["error"] = JsonSerializer.SerializeToElement(answered.Error),
            });

    private static (string Name, string? Value)[] With(
        (string Name, string? Value)[] fields,
        string name,
        string? value) =>
        [
            .. fields.Where(field => !string.Equals(field.Name, name, StringComparison.Ordinal)),
            (name, value),
        ];

    private static (string Name, string? Value)[] Exchanged(OidcClient registered, string grant) =>
    [
        ("grant_type", grant),
        ("client_id", registered.ClientId),
        ("scope", "openid"),
    ];

    private static async ValueTask<Answer> SentAsync(
        HttpClient requests,
        HttpMethod method,
        Uri endpoint,
        IEnumerable<KeyValuePair<string, string>>? fields,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, endpoint);

        if (fields is not null)
        {
            request.Content = new FormUrlEncodedContent(fields);
        }

        using HttpResponseMessage response = await requests
            .SendAsync(request, cancellationToken)
            .ConfigureAwait(false);

        MediaTypeHeaderValue? type = response.Content.Headers.ContentType;

        if (!string.Equals(type?.MediaType, "application/json", StringComparison.OrdinalIgnoreCase))
        {
            return new Answer((int)response.StatusCode, null);
        }

        string body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        using var parsed = JsonDocument.Parse(body);

        return new Answer((int)response.StatusCode, parsed.RootElement.Clone());
    }

    private string Drawn(int length)
    {
        byte[] bytes = new byte[length];

        randomness.GetBytes(bytes);

        return Base64Url.EncodeToString(bytes);
    }

    // Every probe but the discovery, in the order they are asked: the implicit and
    // hybrid forms, the retired grants, the plain method, a challenge naming no method,
    // which RFC 7636 reads as plain, no proof key at all, a destination other than the
    // registered one, which OAuth 2.1 section 2.3.5 fails, and a client that does not
    // authenticate, which is a public client, and none exists.
    private IEnumerable<Probe> Probes(OidcClient registered, Uri push, Uri token)
    {
        string verifier = Drawn(32);

        (string Name, string? Value)[] Pushed() =>
        [
            ("response_type", "code"),
            ("client_id", registered.ClientId),
            ("redirect_uri", registered.Redirect),
            ("scope", "openid"),
            ("state", Drawn(16)),
            ("code_challenge", Base64Url.EncodeToString(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)))),
            ("code_challenge_method", "S256"),
        ];

        foreach (string responseType in Implicit)
        {
            yield return new Probe(
                "push",
                push,
                "response_type",
                With(Pushed(), "response_type", responseType),
                UnsupportedResponseType,
                Authenticated: true);
        }

        foreach (string grant in Retired)
        {
            yield return new Probe(
                "token",
                token,
                "grant_type",
                Exchanged(registered, grant),
                UnsupportedGrantType,
                Authenticated: true);
        }

        yield return new Probe(
            "push",
            push,
            "code_challenge_method",
            With(With(Pushed(), "code_challenge", verifier), "code_challenge_method", "plain"),
            InvalidRequest,
            Authenticated: true);
        yield return new Probe(
            "push",
            push,
            "code_challenge_method",
            With(Pushed(), "code_challenge_method", null),
            InvalidRequest,
            Authenticated: true);
        yield return new Probe(
            "push",
            push,
            "code_challenge",
            With(With(Pushed(), "code_challenge", null), "code_challenge_method", null),
            InvalidRequest,
            Authenticated: true);
        yield return new Probe(
            "push",
            push,
            "redirect_uri",
            With(Pushed(), "redirect_uri", Elsewhere),
            InvalidRequest,
            Authenticated: true);
        yield return new Probe(
            "push",
            push,
            Secret,
            Pushed(),
            InvalidClient,
            Authenticated: false);
    }

    // OPS-SEC-002: the secret is read from the registry for this request alone, so one
    // rotated since the last request is the one presented, and it is held nowhere after.
    private async ValueTask<Result<Answer>> AnsweredAsync(
        HttpClient requests,
        OidcClient registered,
        Probe probe,
        CancellationToken cancellationToken)
    {
        List<KeyValuePair<string, string>> fields =
        [
            .. probe.Fields
                .Where(field => field.Value is not null)
                .Select(field => new KeyValuePair<string, string>(field.Name, field.Value!)),
        ];

        if (!probe.Authenticated)
        {
            return Result.Success(
                await SentAsync(requests, HttpMethod.Post, probe.Endpoint, fields, cancellationToken)
                    .ConfigureAwait(false));
        }

        Error? failure = null;

        byte[] current = (await secrets.CurrentAsync(registered.ClientId, cancellationToken).ConfigureAwait(false))
            .Match(read => read, error => Withheld<byte[]>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure<Answer>(failure);
        }

        try
        {
            fields.Add(new KeyValuePair<string, string>(Secret, Encoding.UTF8.GetString(current)));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(current);
        }

        return Result.Success(
            await SentAsync(requests, HttpMethod.Post, probe.Endpoint, fields, cancellationToken)
                .ConfigureAwait(false));
    }

    // One request of the run: where it goes, the field whose form is asked about, what
    // else it carries, the refusal expected, and whether it presents the secret.
    private sealed record Probe(
        string Name,
        Uri Endpoint,
        string Field,
        (string Name, string? Value)[] Fields,
        string Expected,
        bool Authenticated);

    // What the provider answered: its status and, where it answered in JSON, the
    // document, its error and any reference it handed back.
    private sealed record Answer(int Status, JsonElement? Document)
    {
        public string? Error => Member("error");

        public string? RequestUri => Member("request_uri");

        private string? Member(string name) =>
            Document is JsonElement { ValueKind: JsonValueKind.Object } answered
            && answered.TryGetProperty(name, out JsonElement member)
            && member.ValueKind == JsonValueKind.String
                ? member.GetString()
                : null;
    }
}
