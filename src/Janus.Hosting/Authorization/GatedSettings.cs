using System;
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
/// <param name="require">
/// The gate's answer to a settings change, from the one place a restriction is read
/// and refused.
/// </param>
/// <remarks>
/// Implements IDN-ACCT-007 AC2 and AUTHZ-GATE-006 AC2: the account's operations ask
/// here, and the answer is the gate's, so the restriction is enforced through the gate
/// and in no second place. The gate is named where it is registered and nowhere else
/// (LIB-SEAM-001 AC1).
/// </remarks>
internal sealed class GatedSettings(Func<AccessContext, CancellationToken, ValueTask<Result>> require)
    : ISettingsRestriction
{
    /// <inheritdoc/>
    public async ValueTask<Error?> RefusedAsync(SubjectId subject, CancellationToken cancellationToken) =>
        (await require(AccessContext.Of(subject), cancellationToken).ConfigureAwait(false))
            .Match(() => (Error?)null, error => error);
}
