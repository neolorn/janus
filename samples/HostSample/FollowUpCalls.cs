using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HostSample;

/// <summary>
/// The workshop's job that calls customers after their repairs. A host runs it in the
/// background, never while a request is served, as the job the workshop declares, under
/// a principal of the organization whose repairs it reads (IDN-PRIN-001).
/// </summary>
public sealed class FollowUpCalls
{
    /// <summary>
    /// The job's name, as the workshop declares it.
    /// </summary>
    public const string Name = "follow-up-calls";

    // Why the job acts, recorded with each decision the gate takes for it.
    private const string Reason = "Calls each customer after their repair, under the interest the workshop assessed.";

    private readonly IServiceScopeFactory _scopes;

    internal FollowUpCalls(IServiceScopeFactory scopes) => _scopes = scopes;

    /// <summary>
    /// The context the job acts in for an organization.
    /// </summary>
    /// <param name="organization">The organization whose repairs the job reads.</param>
    /// <returns>The context.</returns>
    public static AccessContext ContextFor(OrganizationId organization) =>
        AccessContext.Of(SystemPrincipal.ForOrganization(Name, Reason, organization));

    /// <summary>
    /// The repairs of an organization whose customers the workshop is to call: each the
    /// gate admits the job to read, whose customer has not objected to the calls.
    /// </summary>
    /// <param name="organization">The organization whose repairs the job reads.</param>
    /// <param name="cancellationToken">Abandons the run.</param>
    /// <returns>Each repair's ticket, or the gate's refusal.</returns>
    public async Task<Result<IReadOnlyList<string>>> DueAsync(OrganizationId organization, CancellationToken cancellationToken)
    {
        AsyncServiceScope scope = _scopes.CreateAsyncScope();

        await using (scope.ConfigureAwait(false))
        {
            IAccessGate gate = scope.ServiceProvider.GetRequiredService<IAccessGate>();
            WorkshopContext context = scope.ServiceProvider.GetRequiredService<WorkshopContext>();

            Result<Expression<Func<Repair, bool>>> filter = await gate
                .FilterAsync(
                    ContextFor(organization),
                    WorkshopModel.ReadRepair,
                    WorkshopModel.RepairType,
                    organization,
                    context.RepairSources(),
                    cancellationToken)
                .ConfigureAwait(false);

            if (filter.Refusal() is Error denied)
            {
                return Result.Failure<IReadOnlyList<string>>(denied);
            }

            List<string> due = await context.Repairs
                .Where(filter.Value())
                .Where(repair => repair.FollowUp)
                .OrderBy(repair => repair.Id)
                .Select(repair => repair.Id)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            return Result.Success<IReadOnlyList<string>>(due);
        }
    }
}
