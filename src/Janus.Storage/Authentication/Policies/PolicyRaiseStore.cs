using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Policies;
using Janus.Core;
using Microsoft.EntityFrameworkCore;

namespace Janus.Storage.Authentication.Policies;

/// <summary>
/// The requirements the policies in force have raised, over the
/// <c>policy_raises</c> table.
/// </summary>
/// <param name="context">The context the operation's writes are tracked on.</param>
/// <param name="time">The clock the row's identifier is drawn against.</param>
/// <remarks>
/// Implements AUTH-FACT-017 and CONV-DESIGN-003. Recording a raise replaces what the
/// scope had raised for that field, so one field of one scope stands for one
/// requirement and one deadline.
/// </remarks>
internal sealed class PolicyRaiseStore(StoreContext context, TimeProvider time)
    : IPolicyRaiseStore
{
    /// <inheritdoc/>
    public async ValueTask<IReadOnlyList<PolicyRaise>> OfAsync(
        OrganizationId? organization,
        CancellationToken cancellationToken) =>
        [.. (await context.PolicyRaises
                .Where(raise => raise.Organization == organization)
                .OrderBy(raise => raise.RaisedAt)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false))
            .Select(raise => new PolicyRaise(raise.Field, raise.Value, raise.RaisedAt))];

    /// <inheritdoc/>
    public async ValueTask RecordAsync(
        OrganizationId? organization,
        IReadOnlyCollection<PolicyRaise> raised,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(raised);

        foreach (PolicyRaise raise in raised)
        {
            await RemoveAsync(organization, raise.Field, cancellationToken).ConfigureAwait(false);

            await context.PolicyRaises
                .AddAsync(
                    new PolicyRaiseRecord
                    {
                        Id = Guid.CreateVersion7(time.GetUtcNow()),
                        Organization = organization,
                        Field = raise.Field,
                        Value = raise.Value,
                        RaisedAt = raise.At,
                    },
                    cancellationToken)
                .ConfigureAwait(false);
        }
    }

    /// <inheritdoc/>
    public async ValueTask RemoveAsync(
        OrganizationId? organization,
        PolicyField field,
        CancellationToken cancellationToken)
    {
        PolicyRaiseRecord? standing = await context.PolicyRaises
            .SingleOrDefaultAsync(
                raise => raise.Organization == organization && raise.Field == field,
                cancellationToken)
            .ConfigureAwait(false);

        if (standing is not null)
        {
            context.PolicyRaises.Remove(standing);
        }
    }
}
