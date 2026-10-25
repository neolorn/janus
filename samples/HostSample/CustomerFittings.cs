using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HostSample;

/// <summary>
/// The grip fittings the workshop holds for one customer, deleted with their
/// registrations when the customer's consent to them ends or the customer is erased.
/// </summary>
internal static class CustomerFittings
{
    /// <summary>
    /// Deletes every grip fitting held for a customer, and removes each one's registration,
    /// in one transaction of a scope of its own.
    /// </summary>
    /// <param name="scopes">Where the scope is made.</param>
    /// <param name="customer">The customer.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>Nothing where the fittings are gone, and the refusal where they are kept.</returns>
    public static async ValueTask<Result> DeleteAsync(
        IServiceScopeFactory scopes,
        SubjectId customer,
        CancellationToken cancellationToken)
    {
        AsyncServiceScope scope = scopes.CreateAsyncScope();

        await using (scope.ConfigureAwait(false))
        {
            IUnitOfWork work = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            IResources resources = scope.ServiceProvider.GetRequiredService<IResources>();
            WorkshopContext context = scope.ServiceProvider.GetRequiredService<WorkshopContext>();

            if ((await work.BeginAsync(cancellationToken).ConfigureAwait(false)).Refusal() is Error notBegun)
            {
                return Result.Failure(notBegun);
            }

            await context.Database.UseUnitOfWorkAsync(work, cancellationToken).ConfigureAwait(false);

            List<string> held = await context.Fittings
                .Where(fitting => fitting.Customer == customer)
                .Select(fitting => fitting.Id)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            foreach (string fitting in held)
            {
                if ((await resources
                        .RemoveAsync(new ResourceReference(WorkshopModel.FittingType, ResourceId.Parse(fitting)), cancellationToken)
                        .ConfigureAwait(false)).Refusal() is Error kept)
                {
                    await work.RollbackAsync().ConfigureAwait(false);

                    return Result.Failure(kept);
                }
            }

            _ = await context.Fittings
                .Where(fitting => fitting.Customer == customer)
                .ExecuteDeleteAsync(cancellationToken)
                .ConfigureAwait(false);

            return await work.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
