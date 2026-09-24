using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.IdentityModel.Protocols;

namespace Janus.Hosting.Credentials;

/// <summary>
/// Where a social provider's documents are read from.
/// </summary>
/// <param name="channel">The framework's factory of clients.</param>
/// <remarks>
/// Implements IDN-LIFE-012a. Each document is read on a client of the framework's
/// factory, which rotates its connections, and only over HTTPS.
/// </remarks>
internal sealed class ProviderDocuments(IHttpClientFactory channel) : IDocumentRetriever
{
    /// <inheritdoc/>
    public async Task<string> GetDocumentAsync(string address, CancellationToken cancel)
    {
        var located = new Uri(address, UriKind.Absolute);

        if (located.Scheme != Uri.UriSchemeHttps)
        {
            throw new InvalidOperationException("A provider's document is read over HTTPS only.");
        }

        using HttpClient requests = channel.CreateClient(ProviderKeys.Channel);

        return await requests.GetStringAsync(located, cancel).ConfigureAwait(false);
    }
}
