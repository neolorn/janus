using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Authentication.Policies;

/// <summary>
/// Where the requirements a policy has raised are kept, so that a sign-in can tell
/// what an account has yet to comply with and when its run-up ends.
/// </summary>
/// <remarks>
/// Implements AUTH-FACT-017 and CONV-DESIGN-003. A raise belongs to the scope whose
/// policy was changed: the deployment's, or one organization's. Lowering a
/// requirement records nothing, so a scope that has only ever loosened holds no rows
/// and every sign-in under it is unheld.
/// </remarks>
internal interface IPolicyRaiseStore
{
    /// <summary>
    /// What one scope has raised.
    /// </summary>
    /// <param name="organization">
    /// Whose policy, or nothing for the deployment's own.
    /// </param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The raises in force, newest last.</returns>
    ValueTask<IReadOnlyList<PolicyRaise>> OfAsync(
        OrganizationId? organization,
        CancellationToken cancellationToken);

    /// <summary>
    /// Records what a change to one scope's policy raised, replacing what that scope
    /// had raised for each field the change touches.
    /// </summary>
    /// <param name="organization">
    /// Whose policy, or nothing for the deployment's own.
    /// </param>
    /// <param name="raised">What the change raised, which may be nothing.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>Nothing.</returns>
    ValueTask RecordAsync(
        OrganizationId? organization,
        IReadOnlyCollection<PolicyRaise> raised,
        CancellationToken cancellationToken);

    /// <summary>
    /// Clears what one scope raised for one field, which a change that lowers or
    /// restores the field does.
    /// </summary>
    /// <param name="organization">
    /// Whose policy, or nothing for the deployment's own.
    /// </param>
    /// <param name="field">Which field.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>Nothing.</returns>
    ValueTask RemoveAsync(
        OrganizationId? organization,
        PolicyField field,
        CancellationToken cancellationToken);
}
