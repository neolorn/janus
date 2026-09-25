using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Tokens;

namespace Janus.Hosting.Credentials;

/// <summary>
/// What a social provider's document names: its issuer, the keys at the address it
/// names for them, and, in a discovery document, where a person signs in and how.
/// </summary>
/// <remarks>
/// Implements IDN-LIFE-012, IDN-LIFE-012a and REG-IDENT-008. Google's Cross-Account
/// Protection configuration and the providers' discovery documents name the issuer and
/// the keys alike; a document that names either wrongly verifies nothing. An endpoint
/// that is not HTTPS is read as not named, and a list of response modes or proof-key
/// methods the document leaves out is read as the discovery standard's default, which
/// holds neither a posted form nor S256.
/// </remarks>
internal sealed class ProviderMetadataReading : IConfigurationRetriever<ProviderMetadata>
{
    /// <inheritdoc/>
    public async Task<ProviderMetadata> GetConfigurationAsync(
        string address,
        IDocumentRetriever retriever,
        CancellationToken cancel)
    {
        ArgumentNullException.ThrowIfNull(retriever);

        using var document = JsonDocument.Parse(
            await retriever.GetDocumentAsync(address, cancel).ConfigureAwait(false));

        string issuer = Named(document.RootElement, "issuer");
        string keys = Named(document.RootElement, "jwks_uri");
        var set = new JsonWebKeySet(await retriever.GetDocumentAsync(keys, cancel).ConfigureAwait(false));

        return new ProviderMetadata(
            issuer,
            [.. set.GetSigningKeys()],
            Endpoint(document.RootElement, "authorization_endpoint"),
            Endpoint(document.RootElement, "token_endpoint"),
            Lists(document.RootElement, "response_modes_supported", "form_post"),
            Lists(document.RootElement, "code_challenge_methods_supported", "S256"));
    }

    private static Uri? Endpoint(JsonElement document, string name) =>
        document.TryGetProperty(name, out JsonElement value)
        && value.ValueKind is JsonValueKind.String
        && Uri.TryCreate(value.GetString(), UriKind.Absolute, out Uri? named)
        && named.Scheme == Uri.UriSchemeHttps
            ? named
            : null;

    private static bool Lists(JsonElement document, string name, string member)
    {
        if (!document.TryGetProperty(name, out JsonElement value)
            || value.ValueKind is not JsonValueKind.Array)
        {
            return false;
        }

        foreach (JsonElement listed in value.EnumerateArray())
        {
            if (listed.ValueKind is JsonValueKind.String
                && string.Equals(listed.GetString(), member, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static string Named(JsonElement document, string name) =>
        document.TryGetProperty(name, out JsonElement value)
        && value.ValueKind is JsonValueKind.String
        && value.GetString() is { Length: > 0 } named
            ? named
            : throw new InvalidOperationException("The provider's document names no " + name + ".");
}
