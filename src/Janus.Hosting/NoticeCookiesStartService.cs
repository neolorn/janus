using System;
using System.Threading;
using System.Threading.Tasks;
using Janus.Hosting.Privacy;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Janus.Hosting;

/// <summary>
/// Compares, at each start, the cookies the deployment sets with those the current
/// version of the privacy notice lists.
/// </summary>
/// <param name="scopes">Where the scope the comparison runs in comes from.</param>
/// <remarks>
/// Implements PRIV-CONS-006a (D-203). A release or a declaration can change the cookies
/// the deployment sets without any key changing, so a start is where that is learned
/// of; a difference is raised under <c>notice.cookies</c> and stops nothing, while a
/// lifetime that cannot be read or an alert that cannot be written is a fault naming
/// the code.
/// </remarks>
internal sealed class NoticeCookiesStartService(IServiceScopeFactory scopes) : IHostedService
{
    /// <inheritdoc/>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = scopes.CreateAsyncScope();
        (await scope.ServiceProvider
                .GetRequiredService<NoticeCookieCheck>()
                .ComparedAsync(cancellationToken))
            .Switch(() => { }, error => throw new InvalidOperationException(error.Code.ToString()));
    }

    /// <inheritdoc/>
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
