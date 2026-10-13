using System.Threading;
using System.Threading.Tasks;
using Janus.Privacy.Bases;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Janus.Hosting;

/// <summary>
/// Writes the declared lawful bases into their table before the web server starts.
/// </summary>
/// <param name="scopes">Where the scope the write runs in comes from.</param>
/// <remarks>
/// Implements PRIV-BASIS-001 (D-183). The table holds exactly the list the host declared
/// once the start has passed, whatever it held before.
/// </remarks>
internal sealed class LawfulBasisStartService(IServiceScopeFactory scopes) : IHostedService
{
    /// <inheritdoc/>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = scopes.CreateAsyncScope();
        await scope.ServiceProvider
            .GetRequiredService<LawfulBasisSeed>()
            .SeededAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
