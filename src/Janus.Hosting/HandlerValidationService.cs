using System.Threading;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Privacy;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Janus.Hosting;

/// <summary>
/// Reads the declared sensitive types and objectable purposes against the handlers
/// the host registered, and the subscribers' names against each other, before the web
/// server starts.
/// </summary>
/// <param name="scopes">Where the scope the check reads in comes from.</param>
/// <remarks>
/// Implements PRIV-RIGHT-005b, PRIV-RIGHT-001a, IDN-LIFE-003a, DR-016 and D-160. A
/// handler that is not there, or answers to another's name, is found now rather than at
/// the erasure that would have reached no one.
/// </remarks>
internal sealed class HandlerValidationService(IServiceScopeFactory scopes) : IHostedService
{
    /// <inheritdoc/>
    /// <exception cref="StartupException">
    /// A sensitive resource type or an objectable purpose has no registered handler, or
    /// a subscriber's name is taken.
    /// </exception>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        using IServiceScope scope = scopes.CreateScope();

        scope.ServiceProvider
            .GetRequiredService<HandlerCoverage>()
            .Validate()
            .Switch(
                () => { },
                failure => throw new StartupException(
                    "A subject-event subscriber's name is taken, or a sensitive resource type or an objectable purpose the deployment declares has no registered handler.",
                    failure));

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
