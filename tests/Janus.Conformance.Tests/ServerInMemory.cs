using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Primitives;

namespace Janus.Conformance.Tests;

/// <summary>
/// The sample host's web server, which takes its requests from a client in the same
/// process rather than from a socket. The host starts it as it starts any web server,
/// after the library's checks, and it hands each request to the pipeline the host
/// built, as a request arriving over HTTPS.
/// </summary>
internal sealed class ServerInMemory : IServer
{
    private readonly Handler _handler;

    private Func<IFeatureCollection, Task>? _serve;

    /// <summary>
    /// Stands the server up, taking nothing until the host starts it.
    /// </summary>
    public ServerInMemory() => _handler = new Handler(this);

    /// <inheritdoc/>
    public IFeatureCollection Features { get; } = new FeatureCollection();

    /// <summary>
    /// A client whose requests this server takes.
    /// </summary>
    /// <param name="origin">The origin the client's relative addresses resolve against.</param>
    /// <returns>The client.</returns>
    public HttpClient Client(Uri origin) => new(_handler, disposeHandler: false) { BaseAddress = origin };

    /// <inheritdoc/>
    public Task StartAsync<TContext>(IHttpApplication<TContext> application, CancellationToken cancellationToken)
        where TContext : notnull
    {
        ArgumentNullException.ThrowIfNull(application);

        _serve = async features =>
        {
            TContext context = application.CreateContext(features);

            try
            {
                await application.ProcessRequestAsync(context);
            }
            catch (Exception fault)
            {
                application.DisposeContext(context, fault);

                throw;
            }

            application.DisposeContext(context, null);
        };

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task StopAsync(CancellationToken cancellationToken)
    {
        _serve = null;

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _serve = null;
        _handler.Dispose();
    }

    private Func<IFeatureCollection, Task> Serving() =>
        _serve ?? throw new InvalidOperationException("The server is not running.");

    // What a request carries as it reaches the pipeline, and what the pipeline wrote
    // back, as a server reads it off the connection and writes it onto it.
    private sealed class Handler(ServerInMemory server) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Uri address = request.RequestUri
                ?? throw new ArgumentException("The request names no address.", nameof(request));

            var requested = new HttpRequestFeature
            {
                Protocol = "HTTP/1.1",
                Scheme = address.Scheme,
                Method = request.Method.Method,
                PathBase = string.Empty,
                Path = Uri.UnescapeDataString(address.AbsolutePath),
                QueryString = address.Query,
                RawTarget = address.PathAndQuery,
                Body = request.Content is null
                    ? Stream.Null
                    : await request.Content.ReadAsStreamAsync(cancellationToken),
            };

            requested.Headers.Host = address.Authority;

            foreach (KeyValuePair<string, IEnumerable<string>> header in request.Headers)
            {
                requested.Headers.Append(header.Key, header.Value.ToArray());
            }

            if (request.Content is not null)
            {
                foreach (KeyValuePair<string, IEnumerable<string>> header in request.Content.Headers)
                {
                    requested.Headers.Append(header.Key, header.Value.ToArray());
                }
            }

            var answered = new HttpResponseFeature();

            await using var written = new MemoryStream();

            var body = new StreamResponseBodyFeature(written);
            var features = new FeatureCollection();

            features.Set<IHttpRequestFeature>(requested);
            features.Set<IHttpRequestBodyDetectionFeature>(new BodyDetection(request.Content is not null));
            features.Set<IHttpResponseFeature>(answered);
            features.Set<IHttpResponseBodyFeature>(body);
            features.Set<IHttpConnectionFeature>(new HttpConnectionFeature
            {
                ConnectionId = Guid.NewGuid().ToString("n"),
                LocalIpAddress = IPAddress.Loopback,
                RemoteIpAddress = IPAddress.Loopback,
            });

            await server.Serving()(features);
            await body.CompleteAsync();

            var response = new HttpResponseMessage((HttpStatusCode)answered.StatusCode)
            {
                RequestMessage = request,
                Content = new ByteArrayContent(written.ToArray()),
            };

            foreach (KeyValuePair<string, StringValues> header in answered.Headers)
            {
                string[] values = [.. header.Value.Select(value => value ?? string.Empty)];

                if (!response.Headers.TryAddWithoutValidation(header.Key, values))
                {
                    _ = response.Content.Headers.TryAddWithoutValidation(header.Key, values);
                }
            }

            return response;
        }
    }

    // Whether the request carries a body, which the framework reads before it reads
    // one.
    private sealed class BodyDetection(bool present) : IHttpRequestBodyDetectionFeature
    {
        public bool CanHaveBody => present;
    }
}
