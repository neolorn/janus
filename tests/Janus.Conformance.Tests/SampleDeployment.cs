using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;

namespace Janus.Conformance.Tests;

/// <summary>
/// One further application of the sample deployment, built for one step-up case of the
/// truth table and stopped when the suite disposes it (LIB-TEST-001 AC2).
/// </summary>
/// <param name="application">The application, started.</param>
/// <param name="server">The web server it runs on.</param>
internal sealed class SampleDeployment(WebApplication application, ServerInMemory server)
    : IServiceProvider, IAsyncDisposable
{
    /// <inheritdoc/>
    public object? GetService(Type serviceType) => application.Services.GetService(serviceType);

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await application.StopAsync(CancellationToken.None);
        await application.DisposeAsync();

        server.Dispose();
    }
}
