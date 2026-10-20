using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HostSample;

/// <summary>
/// The workshop's own work when a customer's consent or objection changes: a withdrawn
/// consent to grip fittings deletes the fittings held for the customer, and an objection
/// to follow-up calls stops them for every repair of the customer's (PRIV-RIGHT-001a).
/// </summary>
/// <param name="scopes">Where each change's scope is made.</param>
internal sealed class WorkshopPurposes(IServiceScopeFactory scopes) : IPurposeHandler
{
    /// <inheritdoc/>
    public string Name => "workshop-purposes";

    /// <inheritdoc/>
    public IReadOnlyCollection<string> Purposes { get; } =
        [WorkshopModel.AdaptingGrips, WorkshopModel.FollowingUpRepairs];

    /// <inheritdoc/>
    public async ValueTask<Result> ConsentChangedAsync(ConsentChanged changed, CancellationToken cancellationToken)
    {
        if (changed is not { Purpose: WorkshopModel.AdaptingGrips, Change: ConsentChange.Withdrawn, Subject: SubjectId customer })
        {
            return Result.Success();
        }

        return await CustomerFittings.DeleteAsync(scopes, customer, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<Result> ObjectionChangedAsync(ObjectionChanged changed, CancellationToken cancellationToken)
    {
        if (changed is not { Purpose: WorkshopModel.FollowingUpRepairs, Objecting: true, Subject: SubjectId customer })
        {
            return Result.Success();
        }

        AsyncServiceScope scope = scopes.CreateAsyncScope();

        await using (scope.ConfigureAwait(false))
        {
            WorkshopContext context = scope.ServiceProvider.GetRequiredService<WorkshopContext>();

            _ = await context.Repairs
                .Where(repair => repair.Customer == customer)
                .ExecuteUpdateAsync(set => set.SetProperty(repair => repair.FollowUp, false), cancellationToken)
                .ConfigureAwait(false);
        }

        return Result.Success();
    }
}
