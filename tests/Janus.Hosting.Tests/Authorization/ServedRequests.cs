using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Hosting.Bff;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Janus.Hosting.Tests.Authorization;

/// <summary>
/// The fixture's deployment serving requests: the library registered over the same
/// database, the host's own branch outside both profiles, the machine profile and the
/// browser profile mounted as a host mounts them, and the host's endpoint at the end of
/// each. Each request is begun and ended by the framework's own context factory, which
/// is what tracks the request a flow serves (CONV-DESIGN-007, D-206).
/// </summary>
internal sealed class ServedRequests : IAsyncDisposable
{
    /// <summary>
    /// A path the browser profile governs.
    /// </summary>
    public const string Browser = "/workspaces/digest";

    /// <summary>
    /// A path the machine profile governs.
    /// </summary>
    public const string Machine = "/callbacks/sms/dlr";

    /// <summary>
    /// A path the host serves outside both profiles.
    /// </summary>
    public const string Outside = "/outside/digest";

    private readonly WebApplication _application;

    private readonly ServerInMemory _server = new();

    private ServedRequests(WebApplication application) => _application = application;

    /// <summary>
    /// The deployment's container, which background work resolves from outside any
    /// request.
    /// </summary>
    public IServiceProvider Services => _application.Services;

    /// <summary>
    /// Starts the deployment, its endpoint running what the test gives it on every path.
    /// </summary>
    /// <param name="fixture">The database the deployment reads.</param>
    /// <param name="endpoint">What the host's endpoint does with the request it serves.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The deployment, serving.</returns>
    public static Task<ServedRequests> StartAsync(
        HostFixture fixture,
        RequestDelegate endpoint,
        CancellationToken cancellationToken) =>
        StartAsync(fixture, HostFixture.Declaration(), endpoint, cancellationToken);

    /// <summary>
    /// Starts the deployment under the declaration given, which is how a case needing the
    /// derivation precomputed into grant rows serves its request (AUTHZ-DERIVE-005).
    /// </summary>
    /// <param name="fixture">The database the deployment reads.</param>
    /// <param name="declaration">The host's declaration.</param>
    /// <param name="endpoint">What the host's endpoint does with the request it serves.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The deployment, serving.</returns>
    public static async Task<ServedRequests> StartAsync(
        HostFixture fixture,
        AuthorizationDeclaration declaration,
        RequestDelegate endpoint,
        CancellationToken cancellationToken)
    {
        WebApplicationBuilder builder = WebApplication.CreateSlimBuilder();

        builder.Logging.ClearProviders();
        builder.Services.AddSingleton<TimeProvider>(new FixedTime(Deployment.Noon));
        builder.Services.AddSingleton<ISecretSource>(HostFixture.Secrets(fixture.MaintenanceConnectionString));
        HostFixture.Sourced(builder.Services, fixture.ConnectionString);
        builder.Services.AddJanus(fixture.ConnectionString, declaration, ApplicationKind.Public);

        WebApplication application = builder.Build();
        var pipeline = (IApplicationBuilder)application;

        _ = pipeline.Map("/outside", outside => outside.Run(endpoint));
        _ = pipeline.UseMachineProfile();
        _ = pipeline.UseBrowserProfile();
        pipeline.Run(endpoint);

        // CONV-DESIGN-007, AUTH-KEY-001: the key ring is filled and the provider's signing
        // keys read before anything is served, which is what their hosted services do as
        // a deployment starts.
        KeyRingService ring = application.Services.GetServices<IHostedService>().OfType<KeyRingService>().Single();
        await ring.StartingAsync(cancellationToken);
        await ring.StartAsync(cancellationToken);
        await application.Services.GetServices<IHostedService>()
            .OfType<ProviderStartService>()
            .Single()
            .StartAsync(cancellationToken);

        var served = new ServedRequests(application);

        await served._server.StartAsync(
            new Served(application.Services.GetRequiredService<IHttpContextFactory>(), pipeline.Build()),
            cancellationToken);

        return served;
    }

    /// <summary>
    /// Serves one request for the path.
    /// </summary>
    /// <param name="path">The request's path.</param>
    /// <returns>What it came back with.</returns>
    public Task<Answer> SendAsync(string path) => _server.SendAsync("GET", path);

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        _server.Dispose();
        await _application.DisposeAsync();
    }

    // What the server runs a request through: the framework's context factory begins
    // and ends it around the pipeline, as the web host's own application does.
    private sealed class Served(IHttpContextFactory contexts, RequestDelegate pipeline) : IHttpApplication<HttpContext>
    {
        public HttpContext CreateContext(IFeatureCollection contextFeatures) => contexts.Create(contextFeatures);

        public Task ProcessRequestAsync(HttpContext context) => pipeline(context);

        public void DisposeContext(HttpContext context, Exception? exception) => contexts.Dispose(context);
    }
}
