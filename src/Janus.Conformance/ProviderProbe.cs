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
using Janus.Core;

namespace Janus.Conformance;

/// <summary>
/// The provider's refusals, asked of the deployment over its own endpoints as a
/// registered client would ask them.
/// </summary>
/// <param name="client">What reaches the deployment.</param>
/// <param name="issuer">The provider's issuer, which its discovery document sits under.</param>
/// <param name="registered">A client the deployment's registry holds.</param>
/// <remarks>
/// Implements AUTH-OIDC-006 AC1 and LIB-TEST-001, as entry 280 of the decisions pending
/// review carries the library's own suite into the one a host runs. Each form the two
/// specifications retire is asked for with everything else in order, so what is
/// refused is the form. The endpoints are the ones the discovery document names, so
/// the suite assumes no route of the deployment's (LIB-HOST-003).
/// </remarks>
internal sealed class ProviderProbe(HttpClient client, Uri issuer, ConformanceClient registered)
{
    private const string UnsupportedResponseType = "unsupported_response_type";

    private const string UnsupportedGrantType = "unsupported_grant_type";

    private const string InvalidRequest = "invalid_request";

    private const string InvalidClient = "invalid_client";

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

    private readonly string _verifier = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));

    /// <summary>
    /// Asks every refusal.
    /// </summary>
    /// <param name="cancellationToken">Abandons the run.</param>
    /// <returns>A finding for each form the provider admitted or advertised.</returns>
    public async ValueTask<ConformanceReport> RunAsync(CancellationToken cancellationToken)
    {
        var findings = new List<ConformanceFinding>();
        var discovery = new Uri(issuer.AbsoluteUri.TrimEnd('/') + "/.well-known/openid-configuration");

        Answer described = await SendAsync(HttpMethod.Get, discovery, fields: null, cancellationToken)
            .ConfigureAwait(false);

        if (described.Document is not JsonElement document
            || Endpoint(document, "pushed_authorization_request_endpoint") is not Uri push
            || Endpoint(document, "token_endpoint") is not Uri token)
        {
            findings.Add(Finding("discovery", "document", sent: null, expected: "served", described));

            return new ConformanceReport(findings);
        }

        findings.AddRange(Advertised(document));

        foreach (string responseType in Implicit)
        {
            await RefusedAsync(
                findings,
                "push",
                push,
                "response_type",
                With(Pushed(), "response_type", responseType),
                UnsupportedResponseType,
                cancellationToken).ConfigureAwait(false);
        }

        foreach (string grant in Retired)
        {
            await RefusedAsync(
                findings,
                "token",
                token,
                "grant_type",
                Exchanged(grant),
                UnsupportedGrantType,
                cancellationToken).ConfigureAwait(false);
        }

        // The plain method, a challenge naming no method, which RFC 7636 reads as
        // plain, and no proof key at all.
        await RefusedAsync(
            findings,
            "push",
            push,
            "code_challenge_method",
            With(With(Pushed(), "code_challenge", _verifier), "code_challenge_method", "plain"),
            InvalidRequest,
            cancellationToken).ConfigureAwait(false);
        await RefusedAsync(
            findings,
            "push",
            push,
            "code_challenge_method",
            With(Pushed(), "code_challenge_method", null),
            InvalidRequest,
            cancellationToken).ConfigureAwait(false);
        await RefusedAsync(
            findings,
            "push",
            push,
            "code_challenge",
            With(With(Pushed(), "code_challenge", null), "code_challenge_method", null),
            InvalidRequest,
            cancellationToken).ConfigureAwait(false);

        // A client that does not authenticate is a public client, and none exists.
        await RefusedAsync(
            findings,
            "push",
            push,
            "client_secret",
            With(Pushed(), "client_secret", null),
            InvalidClient,
            cancellationToken).ConfigureAwait(false);

        return new ConformanceReport(findings);
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
    private static IEnumerable<ConformanceFinding> Advertised(JsonElement document)
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

    private static ConformanceFinding Listing(JsonElement document, string member) =>
        new(
            ConformanceCheck.Provider,
            new Error(
                ErrorCodes.ProviderNonconformant,
                new Dictionary<string, JsonElement>(StringComparer.Ordinal)
                {
                    ["probe"] = JsonSerializer.SerializeToElement("discovery"),
                    ["member"] = JsonSerializer.SerializeToElement(member),
                    ["listed"] = document.TryGetProperty(member, out JsonElement listed)
                        ? listed.Clone()
                        : JsonSerializer.SerializeToElement<string?>(null),
                }));

    private static ConformanceFinding Finding(
        string probe,
        string field,
        string? sent,
        string expected,
        Answer answered) =>
        new(
            ConformanceCheck.Provider,
            new Error(
                ErrorCodes.ProviderNonconformant,
                new Dictionary<string, JsonElement>(StringComparer.Ordinal)
                {
                    ["probe"] = JsonSerializer.SerializeToElement(probe),
                    ["field"] = JsonSerializer.SerializeToElement(field),
                    ["sent"] = JsonSerializer.SerializeToElement(sent),
                    ["expected"] = JsonSerializer.SerializeToElement(expected),
                    ["status"] = JsonSerializer.SerializeToElement(answered.Status),
                    ["error"] = JsonSerializer.SerializeToElement(answered.Error),
                }));

    private static (string Name, string? Value)[] With(
        (string Name, string? Value)[] fields,
        string name,
        string? value) =>
        [
            .. fields.Where(field => !string.Equals(field.Name, name, StringComparison.Ordinal)),
            (name, value),
        ];

    private static string? Sent((string Name, string? Value)[] fields, string name) =>
        fields.FirstOrDefault(field => string.Equals(field.Name, name, StringComparison.Ordinal)).Value;

    private (string Name, string? Value)[] Pushed() =>
    [
        ("response_type", "code"),
        ("client_id", registered.ClientId),
        ("client_secret", Encoding.UTF8.GetString(registered.Secret.Span)),
        ("redirect_uri", registered.Destination.AbsoluteUri),
        ("scope", "openid"),
        ("state", Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(16))),
        ("code_challenge", Base64Url.EncodeToString(SHA256.HashData(Encoding.ASCII.GetBytes(_verifier)))),
        ("code_challenge_method", "S256"),
    ];

    private (string Name, string? Value)[] Exchanged(string grant) =>
    [
        ("grant_type", grant),
        ("client_id", registered.ClientId),
        ("client_secret", Encoding.UTF8.GetString(registered.Secret.Span)),
        ("scope", "openid"),
    ];

    private async ValueTask RefusedAsync(
        List<ConformanceFinding> findings,
        string probe,
        Uri endpoint,
        string field,
        (string Name, string? Value)[] fields,
        string expected,
        CancellationToken cancellationToken)
    {
        Answer answered = await SendAsync(HttpMethod.Post, endpoint, fields, cancellationToken).ConfigureAwait(false);

        if (!string.Equals(answered.Error, expected, StringComparison.Ordinal))
        {
            findings.Add(Finding(probe, field, Sent(fields, field), expected, answered));
        }
    }

    private async ValueTask<Answer> SendAsync(
        HttpMethod method,
        Uri endpoint,
        (string Name, string? Value)[]? fields,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, endpoint);

        if (fields is not null)
        {
            request.Content = new FormUrlEncodedContent(
                fields
                    .Where(field => field.Value is not null)
                    .Select(field => new KeyValuePair<string, string>(field.Name, field.Value!)));
        }

        using HttpResponseMessage response = await client
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

    // What the provider answered: its status and, where it answered in JSON, the
    // document.
    private sealed record Answer(int Status, JsonElement? Document)
    {
        public string? Error =>
            Document is JsonElement { ValueKind: JsonValueKind.Object } answered
            && answered.TryGetProperty("error", out JsonElement error)
            && error.ValueKind == JsonValueKind.String
                ? error.GetString()
                : null;
    }
}
