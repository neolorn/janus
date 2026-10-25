using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Net.Http.Headers;

namespace Janus.Hosting.Tests;

/// <summary>
/// A web server that listens on nothing: the host starts it as it starts any server,
/// and a test hands it a request, which it runs through the application the host gave
/// it, the framework's own per-request work around the pipeline included.
/// </summary>
/// <remarks>Implements CONV-TEST-007: a fake, written by hand, never a mock.</remarks>
internal sealed class ServerInMemory : IServer
{
    private Func<IFeatureCollection, Task>? _application;

    /// <inheritdoc/>
    public IFeatureCollection Features { get; } = new FeatureCollection();

    /// <summary>
    /// Whether the host started the server.
    /// </summary>
    public bool Started => _application is not null;

    /// <inheritdoc/>
    public Task StartAsync<TContext>(IHttpApplication<TContext> application, CancellationToken cancellationToken)
        where TContext : notnull
    {
        _application = async features =>
        {
            TContext context = application.CreateContext(features);
            Exception? fault = null;

            try
            {
                await application.ProcessRequestAsync(context);
            }
            catch (Exception thrown)
            {
                fault = thrown;

                throw;
            }
            finally
            {
                // As a web server ends a request, whichever way it went: what the
                // framework registered on the response's completion runs first, the
                // disposal of the request's scope among it, and then the context is
                // disposed.
                await ((ResponseInMemory)features.Get<IHttpResponseFeature>()!).CompletedAsync();
                application.DisposeContext(context, fault);
            }
        };

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <inheritdoc/>
    public void Dispose()
    {
        // Nothing is held open: the server listens on nothing.
    }

    /// <summary>
    /// Runs one request through the application the host started the server with.
    /// </summary>
    /// <param name="method">The request's method.</param>
    /// <param name="path">The request's path.</param>
    /// <param name="query">The request's query string, with its question mark, or nothing.</param>
    /// <param name="cookie">The <c>Cookie</c> header the browser sends, or nothing.</param>
    /// <returns>What the request came back with.</returns>
    /// <exception cref="InvalidOperationException">The host has not started the server.</exception>
    public async Task<Answer> SendAsync(string method, string path, string query = "", string? cookie = null)
    {
        Func<IFeatureCollection, Task> application = _application
            ?? throw new InvalidOperationException("The host has not started the server.");

        var request = new HttpRequestFeature
        {
            Method = method,
            Scheme = "https",
            Protocol = "HTTP/1.1",
            Path = path,
            QueryString = query,
        };

        request.Headers.Host = "identity.example.test";

        if (cookie is not null)
        {
            request.Headers.Cookie = cookie;
        }

        var response = new ResponseInMemory();
        using var body = new MemoryStream();
        var features = new FeatureCollection();

        features.Set<IHttpRequestFeature>(request);
        features.Set<IHttpResponseFeature>(response);
        features.Set<IHttpResponseBodyFeature>(new StreamResponseBodyFeature(body));

        await application(features);

        return new Answer(
            response.StatusCode,
            Encoding.UTF8.GetString(body.ToArray()),
            response.Headers.Location.FirstOrDefault(),
            [.. response.Headers.SetCookie.Select(written => written ?? string.Empty)])
        {
            Headers = response.Headers.ToDictionary(
                header => header.Key,
                header => header.Value.ToString(),
                StringComparer.OrdinalIgnoreCase),
        };
    }

    // The response as the pipeline writes it, holding what is registered to run once it
    // has been sent, which runs last registered first.
    private sealed class ResponseInMemory : HttpResponseFeature
    {
        private readonly Stack<(Func<object, Task> Callback, object State)> _completed = new();

        public override void OnCompleted(Func<object, Task> callback, object state) => _completed.Push((callback, state));

        // Runs what was registered, each once.
        public async Task CompletedAsync()
        {
            while (_completed.TryPop(out (Func<object, Task> Callback, object State) entry))
            {
                await entry.Callback(entry.State);
            }
        }
    }
}
