using System;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authorization.Grants;
using Janus.Core;
using Microsoft.EntityFrameworkCore;

namespace Janus.Storage.Authorization.Grants;

/// <summary>
/// The removals of access in an organization, held one after the other by a lock of the
/// transaction's.
/// </summary>
/// <param name="context">The context whose transaction the lock is held to the end of.</param>
/// <remarks>
/// Implements AUTHZ-GRANT-005 and CONV-DESIGN-003 (D-281). No row stands for every
/// removal an organization may see, so the lock is the organization's own, taken on its
/// identifier and released at commit or rollback; the host needs nothing of its own for
/// two removals at once to be judged in turn.
/// </remarks>
internal sealed class RemovalLocks(StoreContext context) : IRemovalLocks
{
    /// <inheritdoc/>
    /// <exception cref="InvalidOperationException">No transaction is open.</exception>
    public async ValueTask HoldAsync(OrganizationId organization, CancellationToken cancellationToken)
    {
        if (context.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException("An organization's removals are held only inside a transaction.");
        }

        string held = "identity.access_removals/" + organization.Value.ToString();

        _ = await context.Database
            .ExecuteSqlAsync($"SELECT pg_advisory_xact_lock(hashtextextended({held}, 0))", cancellationToken)
            .ConfigureAwait(false);
    }
}
