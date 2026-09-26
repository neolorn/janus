using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.BreakGlass;
using Janus.Core;
using Microsoft.EntityFrameworkCore;

namespace Janus.Storage.Authentication.BreakGlass;

/// <summary>
/// Which account the break-glass session belongs to, over the <c>accounts</c> table.
/// </summary>
/// <param name="context">The context the read runs on.</param>
/// <remarks>
/// Implements OPS-BOOT-002 and CONV-DESIGN-003. The unique partial index on the mark
/// admits one such row, so a second is never read.
/// </remarks>
internal sealed class EmergencyAccount(StoreContext context) : IEmergencyAccount
{
    /// <inheritdoc/>
    public async ValueTask<SubjectId?> FindAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<SubjectId> marked = await context.Accounts
            .Where(account => account.IsEmergency)
            .Select(account => account.Subject)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return marked is [SubjectId emergency] ? emergency : null;
    }
}
