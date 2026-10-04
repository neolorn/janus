using System.Threading;
using System.Threading.Tasks;
using Janus.Privacy.Consents;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Janus.Hosting;

/// <summary>
/// Ends, before the web server starts, every live consent recorded against a document
/// its purpose no longer names.
/// </summary>
/// <param name="scopes">Where the scope the write runs in comes from.</param>
/// <remarks>
/// Implements PRIV-CONS-007 (D-183). The declaration is what names a purpose's
/// document, so a move is learned of here; each consent it leaves behind is stamped
/// superseded and announced once, however many processes start.
/// </remarks>
internal sealed class DocumentSupersessionStartService(IServiceScopeFactory scopes) : IHostedService
{
    /// <inheritdoc/>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = scopes.CreateAsyncScope();
        _ = await scope.ServiceProvider
            .GetRequiredService<DocumentSupersession>()
            .SupersededAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
