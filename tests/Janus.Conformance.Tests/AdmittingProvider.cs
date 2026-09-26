using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Janus.Conformance.Tests;

/// <summary>
/// A provider that admits every request and advertises the forms it should refuse:
/// its discovery document lists them, and every pushed request and every token
/// request is answered as accepted.
/// </summary>
internal sealed class AdmittingProvider : HttpMessageHandler
{
    private const string Discovery =
        """
        {
          "issuer": "https://admitting.example.test",
          "pushed_authorization_request_endpoint": "https://admitting.example.test/par",
          "token_endpoint": "https://admitting.example.test/token",
          "response_types_supported": ["code", "token"],
          "code_challenge_methods_supported": ["plain", "S256"],
          "grant_types_supported": ["authorization_code", "refresh_token", "password"],
          "require_pushed_authorization_requests": false,
          "token_endpoint_auth_methods_supported": ["client_secret_post", "none"]
        }
        """;

    private const string Admitted =
        """
        { "request_uri": "urn:ietf:params:oauth:request_uri:admitted", "expires_in": 60 }
        """;

    /// <summary>
    /// The provider's issuer.
    /// </summary>
    public static Uri Issuer { get; } = new("https://admitting.example.test");

    /// <inheritdoc/>
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken) =>
        Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            RequestMessage = request,
            Content = new StringContent(
                request.Method == HttpMethod.Get ? Discovery : Admitted,
                Encoding.UTF8,
                "application/json"),
        });
}
