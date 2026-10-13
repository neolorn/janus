using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authorization.Roles;
using Janus.Core;
using Microsoft.EntityFrameworkCore;

namespace Janus.Storage.Authorization.Roles;

/// <summary>
/// Whether anything names a role, over the <c>grants</c> and <c>invitations</c> tables.
/// </summary>
/// <param name="context">The context the read runs on.</param>
/// <remarks>
/// Implements AUTHZ-GRANT-004, AUTHZ-GRANT-003 AC3, REG-INV-001 and CONV-DESIGN-003.
/// An invitation's roles are a list on its row rather than rows of their own, so no
/// foreign key keeps the role in place and this read is what does.
/// </remarks>
internal sealed class RoleReferences(StoreContext context) : IRoleReferences
{
    /// <inheritdoc/>
    public async ValueTask<bool> NamedAsync(RoleName role, CancellationToken cancellationToken)
    {
        if (await context.Grants.AnyAsync(row => row.Role == role, cancellationToken).ConfigureAwait(false))
        {
            return true;
        }

        string name = role.ToString();

        return await context.Invitations
            .AnyAsync(
                row => row.AcknowledgedAt == null && row.RevokedAt == null && row.Roles.Contains(name),
                cancellationToken)
            .ConfigureAwait(false);
    }
}
