using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authorization.Gate;
using Janus.Core;
using Janus.Storage.Identity.Accounts;
using Microsoft.EntityFrameworkCore;

namespace Janus.Storage.Authorization.Gate;

/// <summary>
/// Whether an account's processing is restricted, over the <c>accounts</c> table.
/// </summary>
/// <param name="context">The context the row is read on.</param>
/// <remarks>
/// Implements AUTHZ-GATE-006 and CONV-DESIGN-003. It reads the one column the gate
/// evaluates and nothing else of the account.
/// </remarks>
internal sealed class SubjectRestrictions(StoreContext context) : ISubjectRestrictions
{
    /// <inheritdoc/>
    public async ValueTask<bool> IsRestrictedAsync(
        SubjectId subject,
        CancellationToken cancellationToken) =>
        await context.Set<AccountRecord>()
            .AsNoTracking()
            .AnyAsync(
                account => account.Subject == subject && account.State == AccountState.Restricted,
                cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc/>
    public async ValueTask<bool?> HoldAsync(SubjectId subject, CancellationToken cancellationToken)
    {
        // The transaction is the scope's, whoever opened it: an operation of the
        // library, or a host that began the unit of work its own action writes in.
        if (context.Database.CurrentTransaction is null)
        {
            return null;
        }

        return await AccountStore.SharedAsync(context, subject, cancellationToken).ConfigureAwait(false)
            is AccountState.Restricted;
    }
}
