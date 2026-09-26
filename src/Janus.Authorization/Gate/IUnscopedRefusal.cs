using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Authorization.Gate;

/// <summary>
/// Where an operation on a row the deployment does not hold asks the gate for its
/// refusal.
/// </summary>
/// <remarks>
/// Implements CONV-DESIGN-002 AC3 and AUTHZ-SCOPE-001. An operation is authorized in the
/// organization its row belongs to, and a row the deployment does not hold belongs to
/// none, so no grant reaches it. The refusal is the gate's: a restriction first
/// (AUTHZ-GATE-006), then a refusal recorded under an identifier as every other is
/// (AUTHZ-CONCEAL-004), so it reads as the refusal of a caller who holds nothing where a
/// row is. The gate is named where it is registered and nowhere else (LIB-SEAM-001
/// AC1), so the operations ask through this port.
/// </remarks>
internal interface IUnscopedRefusal
{
    /// <summary>
    /// The refusal of a permission asked over what belongs to no organization.
    /// </summary>
    /// <param name="context">Who is asking.</param>
    /// <param name="permission">What they asked for.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// <c>authz.restricted</c> for a restricted account; otherwise <c>authz.denied</c>
    /// carrying the identifier it was recorded under.
    /// </returns>
    ValueTask<Error> RefusedAsync(AccessContext context, Permission permission, CancellationToken cancellationToken);
}
