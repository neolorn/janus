using System;
using System.Threading;
using System.Threading.Tasks;
using Janus.Testing;
using Microsoft.AspNetCore.Builder;

namespace HostSample.Tests;

/// <summary>
/// The workshop started a second time on a deployment, at an origin of its own, as a
/// second application whose sign-on reaches the first (LIB-TEST-004, D-229).
/// </summary>
/// <param name="application">The second application, started.</param>
/// <param name="server">The web server it is started on.</param>
/// <param name="people">The people of the second application, for its browsers.</param>
internal sealed class SecondApplication(WebApplication application, ServerInMemory server, TestPeople people)
    : IAsyncDisposable
{
    /// <summary>
    /// A browser at the second application that holds nothing yet, which makes its
    /// first contact there before its first request.
    /// </summary>
    /// <returns>The browser.</returns>
    public TestVisitor Visitor() => people.Visitor();

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await application.StopAsync(CancellationToken.None);
        await application.DisposeAsync();

        server.Dispose();
    }
}
