using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Janus.Hosting.Tests;

/// <summary>
/// The server that listens on nothing ends each request as a web server ends one, so
/// what the request's scope holds is released with it.
/// </summary>
[Trait("kind", "unit")]
public sealed class ServerInMemoryTests
{
    /// <summary>
    /// What a request resolved from its scope is disposed once the request has been
    /// answered, for each request served.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task SendAsync_ARequestThatResolvesFromItsScope_DisposesWhatItResolvedAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        var disposed = new Disposals();
        using var server = new ServerInMemory();
        using IHost deployment = new HostBuilder()
            .ConfigureWebHost(web => web
                .UseServer(server)
                .Configure(application => application.Run(context =>
                {
                    _ = context.RequestServices.GetRequiredService<Held>();

                    return Task.CompletedTask;
                })))
            .ConfigureServices(services => services.AddScoped(_ => new Held(disposed)))
            .Build();

        await deployment.StartAsync(cancellationToken);

        Answer first = await server.SendAsync("GET", "/first");
        Answer second = await server.SendAsync("GET", "/second");

        await deployment.StopAsync(cancellationToken);

        Assert.Equal(StatusCodes.Status200OK, first.Status);
        Assert.Equal(StatusCodes.Status200OK, second.Status);
        Assert.Equal(2, disposed.Count);
    }

    // How many of what requests resolved have been disposed.
    private sealed class Disposals
    {
        public int Count { get; set; }
    }

    // What a request resolves from its scope, as the library's context is.
    private sealed class Held(Disposals disposals) : IDisposable
    {
        public void Dispose() => disposals.Count++;
    }
}
