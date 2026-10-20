using System.Threading;
using System.Threading.Tasks;
using Janus.Authorization.Roles;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Janus.Hosting;

/// <summary>
/// Defines, before the web server starts, each role the host declared that no start has
/// seeded yet.
/// </summary>
/// <param name="scopes">Where the scope the seeding runs in comes from.</param>
/// <remarks>
/// Implements AUTHZ-GRANT-004 (D-204). The start step runs as <c>role-declaration</c>,
/// and is the one way a declared role is defined.
/// </remarks>
internal sealed class DeclaredRoleStartService(IServiceScopeFactory scopes) : IHostedService
{
    /// <inheritdoc/>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = scopes.CreateAsyncScope();
        await scope.ServiceProvider
            .GetRequiredService<DeclaredRoleSeed>()
            .SeededAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
