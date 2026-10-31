using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Janus.Hosting.Tests.Oidc;

/// <summary>
/// The provider of a test host on a real port: a web server listening over TLS on every
/// address of the machine, on a port it was given, which hands each request it receives
/// to the deployment it serves, as the deployment's own web server would (INT-MAIL-004
/// AC4, D-224).
/// </summary>
internal sealed class ProviderListener : IAsyncDisposable
{
    private readonly WebApplication _listener;
    private readonly ConcurrentQueue<string> _asked = new();
    private Deployment? _serving;

    private ProviderListener(WebApplication listener) => _listener = listener;

    /// <summary>
    /// Gets the port the listener was given.
    /// </summary>
    public int Port { get; private set; }

    /// <summary>
    /// Gets the path of every request the listener received, in the order it received
    /// them.
    /// </summary>
    public IReadOnlyList<string> Asked => [.. _asked];

    /// <summary>
    /// Starts a listener that presents a certificate.
    /// </summary>
    /// <param name="certificate">The certificate, with its key.</param>
    /// <returns>The listener, serving nothing until it is given a deployment.</returns>
    public static async Task<ProviderListener> StartedAsync(X509Certificate2 certificate)
    {
        WebApplicationBuilder builder = WebApplication.CreateSlimBuilder();

        builder.Logging.ClearProviders();
        _ = builder.WebHost.ConfigureKestrel(server =>
            server.Listen(IPAddress.Any, 0, listening => listening.UseHttps(certificate)));

        var listener = new ProviderListener(builder.Build());

        ((IApplicationBuilder)listener._listener).Run(listener.AnsweredAsync);
        await listener._listener.StartAsync(TestContext.Current.CancellationToken);
        listener.Port = new Uri(listener._listener.Urls.Single()).Port;

        return listener;
    }

    /// <summary>
    /// Serves a deployment from now on.
    /// </summary>
    /// <param name="deployment">The deployment.</param>
    public void Serve(Deployment deployment) => _serving = deployment;

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await _listener.StopAsync(TestContext.Current.CancellationToken);
        await _listener.DisposeAsync();
    }

    private async Task AnsweredAsync(HttpContext context)
    {
        _asked.Enqueue(context.Request.Path.Value ?? string.Empty);

        if (_serving is null)
        {
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;

            return;
        }

        await _serving.SendAsync((DefaultHttpContext)context);
    }
}
