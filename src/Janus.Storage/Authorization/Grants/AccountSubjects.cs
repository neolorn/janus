using System.Threading;
using System.Threading.Tasks;
using Janus.Authorization.Grants;
using Janus.Core;
using Janus.Storage.Identity.Accounts;
using Microsoft.EntityFrameworkCore;

namespace Janus.Storage.Authorization.Grants;

/// <summary>
/// Whether an account bears a subject, over the <c>accounts</c> table.
/// </summary>
/// <param name="context">The context the row is read on.</param>
/// <remarks>
/// Implements IDN-ACCT-007, REC-REC-001 and CONV-DESIGN-003. It reads whether the row
/// exists and nothing else of the account.
/// </remarks>
internal sealed class AccountSubjects(StoreContext context) : IAccountSubjects
{
    /// <inheritdoc/>
    public async ValueTask<bool> BearsAsync(SubjectId subject, CancellationToken cancellationToken) =>
        await context.Set<AccountRecord>()
            .AsNoTracking()
            .AnyAsync(account => account.Subject == subject, cancellationToken)
            .ConfigureAwait(false);
}
