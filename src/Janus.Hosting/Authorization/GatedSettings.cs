using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Accounts;
using Janus.Authorization.Gate;
using Janus.Core;

namespace Janus.Hosting.Authorization;

/// <summary>
/// The processing restriction on an account's own settings, as the access gate
/// answers it.
/// </summary>
/// <param name="gate">
/// The gate's answer to a settings change, from the one place a restriction is read
/// and refused.
/// </param>
/// <remarks>
/// Implements IDN-ACCT-007 AC2, AUTHZ-GATE-006 AC2 and CONV-DESIGN-007: the account's
/// operations ask here, and the answer is the gate's, so the restriction is enforced
/// through the gate and in no second place. The two areas cannot reference each other,
/// so the bridge is this project's, and it asks the gate through the contract its own
/// project declares (LIB-SEAM-001 AC1).
/// </remarks>
internal sealed class GatedSettings(ISettingsChangeGate gate) : ISettingsRestriction
{
    /// <inheritdoc/>
    public async ValueTask<Error?> RefusedAsync(AccessContext context, CancellationToken cancellationToken) =>
        (await gate.RequireSettingsChangeAsync(context, cancellationToken).ConfigureAwait(false))
            .Match(() => (Error?)null, error => error);
}
