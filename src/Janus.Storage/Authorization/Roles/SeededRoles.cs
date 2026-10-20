using System;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authorization.Roles;
using Janus.Core;
using Microsoft.EntityFrameworkCore;

namespace Janus.Storage.Authorization.Roles;

/// <summary>
/// The names of the declared roles a start has seeded, over the <c>seeded_roles</c>
/// table.
/// </summary>
/// <param name="context">The context the operation runs on.</param>
/// <remarks>
/// Implements AUTHZ-GRANT-004 and CONV-DESIGN-003 (D-204). A name not yet seeded has no
/// row to lock, so the name itself is held for the rest of the transaction; no read
/// takes this lock.
/// </remarks>
internal sealed class SeededRoles(StoreContext context) : ISeededRoles
{
    /// <inheritdoc/>
    public async ValueTask HoldAsync(RoleName name, CancellationToken cancellationToken)
    {
        if (context.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException("A declared role's name is held only inside the operation's transaction.");
        }

        string held = "identity.seeded_roles/" + name;

        _ = await context.Database
            .ExecuteSqlAsync($"SELECT pg_advisory_xact_lock(hashtextextended({held}, 0))", cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<bool> SeededAsync(RoleName name, CancellationToken cancellationToken) =>
        await context.SeededRoles
            .AnyAsync(row => row.Name == name, cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc/>
    public async ValueTask RecordAsync(RoleName name, CancellationToken cancellationToken) =>
        await context.SeededRoles
            .AddAsync(new SeededRoleRecord { Name = name }, cancellationToken)
            .ConfigureAwait(false);
}
