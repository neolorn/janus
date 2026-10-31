using System;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HostSample;

/// <summary>
/// The workshop's duty board, which takes every alert the library raises and pins it for
/// whoever is on duty (OPS-ALERT-003). An alert handed over again under the same key is
/// the one already pinned, and is pinned once.
/// </summary>
/// <param name="scopes">Where each hand-over's scope is made.</param>
internal sealed class DutyBoard(IServiceScopeFactory scopes) : IAlertHandler
{
    /// <inheritdoc/>
    public async ValueTask<Result> TakeAsync(AlertRaised alert, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(alert);

        AsyncServiceScope scope = scopes.CreateAsyncScope();

        await using (scope.ConfigureAwait(false))
        {
            WorkshopContext workshop = scope.ServiceProvider.GetRequiredService<WorkshopContext>();

            if (await workshop.Board.AnyAsync(pinned => pinned.Key == alert.IdempotencyKey, cancellationToken).ConfigureAwait(false))
            {
                return Result.Success();
            }

            _ = workshop.Board.Add(new PinnedAlert
            {
                Key = alert.IdempotencyKey,
                Condition = alert.Condition.ToString(),
                Severity = alert.Severity.ToString(),
                Scope = alert.Scope,
                RaisedAt = alert.RaisedAt,
            });

            _ = await workshop.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        return Result.Success();
    }
}
