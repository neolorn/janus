using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Tokens;

namespace Janus.Hosting.Credentials;

/// <summary>
/// What a social provider's document names: its issuer, and the keys at the address it
/// names for them.
/// </summary>
/// <remarks>
/// Implements IDN-LIFE-012a. Google's Cross-Account Protection configuration and Apple's
/// discovery document both name the two alike; a document that names either wrongly
/// verifies nothing.
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

        return new ProviderMetadata(issuer, [.. set.GetSigningKeys()]);
    }

    private static string Named(JsonElement document, string name) =>
        document.TryGetProperty(name, out JsonElement value)
        && value.ValueKind is JsonValueKind.String
        && value.GetString() is { Length: > 0 } named
            ? named
            : throw new InvalidOperationException("The provider's document names no " + name + ".");
}
