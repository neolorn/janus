using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authorization.Gate;
using Janus.Core;
using Janus.Storage.Identity.Accounts;
using Microsoft.EntityFrameworkCore;

namespace Janus.Storage.Authorization.Gate;

/// <summary>
/// Which accounts are in a state to exercise a permission, over the <c>accounts</c>
/// table.
/// </summary>
/// <param name="context">The context the rows are read on.</param>
/// <remarks>
/// Implements AUTHZ-GATE-007 and CONV-DESIGN-003. It reads the subject and the state and
/// nothing else of the account, in one statement for every subject asked.
/// </remarks>
internal sealed class ExercisingAccounts(StoreContext context) : IExercisingAccounts
{
    /// <inheritdoc/>
    public async ValueTask<IReadOnlySet<SubjectId>> ExercisingAsync(
        IReadOnlyCollection<SubjectId> subjects,
        bool reading,
        CancellationToken cancellationToken)
    {
        if (subjects.Count is 0)
        {
            return new HashSet<SubjectId>();
        }

        return (await context.Set<AccountRecord>()
                .AsNoTracking()
                .Where(account => subjects.Contains(account.Subject)
                    && (account.State == AccountState.Active
                        || (reading && account.State == AccountState.Restricted)))
                .Select(account => account.Subject)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false))
            .ToHashSet();
    }
}
