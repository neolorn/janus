using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authorization.Resources;
using Janus.Core;
using Janus.Storage.Identity.Accounts;
using Microsoft.EntityFrameworkCore;

namespace Janus.Storage.Authorization.Resources;

/// <summary>
/// Which subjects hold an account, over the <c>accounts</c> table.
/// </summary>
/// <param name="context">The context the rows are read on.</param>
/// <remarks>
/// Implements IDN-LIFE-002a AC1 and CONV-DESIGN-003. It reads the subject and the
/// state and nothing else of the account, in one statement for every subject asked.
/// </remarks>
internal sealed class AccountHolders(StoreContext context) : IAccountHolders
{
    /// <inheritdoc/>
    public async ValueTask<IReadOnlySet<SubjectId>> HoldingAsync(
        IReadOnlyCollection<SubjectId> subjects,
        CancellationToken cancellationToken)
    {
        if (subjects.Count is 0)
        {
            return new HashSet<SubjectId>();
        }

        return (await context.Set<AccountRecord>()
                .AsNoTracking()
                .Where(account => subjects.Contains(account.Subject)
                    && account.State != AccountState.Deleting
                    && account.State != AccountState.Deleted)
                .Select(account => account.Subject)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false))
            .ToHashSet();
    }
}
