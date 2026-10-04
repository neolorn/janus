using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Authorization.Gate;

/// <summary>
/// Where a change to an account's own settings is asked of the gate.
/// </summary>
/// <remarks>
/// Implements CONV-DESIGN-007 and AUTHZ-GATE-006 AC2. The restriction on an account's
/// own settings is the gate's to read and refuse, and no public contract declares the
/// question, so the type that carries it to the account's operations asks through this
/// contract and never names what answers it (LIB-SEAM-001 AC1).
/// </remarks>
internal interface ISettingsChangeGate
{
    /// <summary>
    /// Whether the principal may change its own account's settings.
    /// </summary>
    /// <param name="context">Who is acting, and for whom.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>Success, or <c>authz.restricted</c> while the account's processing is restricted.</returns>
    ValueTask<Result> RequireSettingsChangeAsync(AccessContext context, CancellationToken cancellationToken);
}
