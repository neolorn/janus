using System;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Authentication.Invitations;

/// <summary>
/// The grants an ended membership revokes and the groups it takes the account out of.
/// </summary>
/// <remarks>
/// Implements IDN-MEM-001, AUTHZ-GRANT-005 and CONV-LAYOUT-001 (D-213). The grants and
/// groups are the authorization area's; this is what ending a membership asks of them. The
/// end reaches the account's live grants a person gave in the organization and the
/// organization's groups naming the account; for the administrative organization, the
/// same in every organization the account holds no current membership of.
/// </remarks>
internal interface IMembershipRevocations
{
    /// <summary>
    /// Judges the end as its revocations and removals are judged: the actor may hand out
    /// every role they reach. None is refused as widening the actor's own access.
    /// </summary>
    /// <param name="context">Who ends the membership.</param>
    /// <param name="member">Whose membership.</param>
    /// <param name="organization">Of which organization.</param>
    /// <param name="administrative">Whether the organization administers the deployment.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// Nothing where the end is admitted; otherwise <c>authz.role.notassignable</c>,
    /// recorded as a refusal.
    /// </returns>
    ValueTask<Error?> RefusedAsync(
        AccessContext context,
        SubjectId member,
        OrganizationId organization,
        bool administrative,
        CancellationToken cancellationToken);

    /// <summary>
    /// Revokes the grants the end reaches and takes the account out of the groups it
    /// reaches, inside the transaction the end runs in, recording each with the end's
    /// reason and actor.
    /// </summary>
    /// <param name="member">Whose membership ended.</param>
    /// <param name="organization">Of which organization.</param>
    /// <param name="administrative">Whether the organization administers the deployment.</param>
    /// <param name="actor">Who ended it, or set the end the job carries out.</param>
    /// <param name="reason">Why.</param>
    /// <param name="breakGlassReason">
    /// The reason given at the use of the break-glass credential, where the end was made
    /// in the session it opened, or nothing.
    /// </param>
    /// <param name="at">When.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of revoking and removing.</returns>
    /// <exception cref="InvalidOperationException">No transaction is open.</exception>
    ValueTask RevokeAsync(
        SubjectId member,
        OrganizationId organization,
        bool administrative,
        SubjectId actor,
        string reason,
        string? breakGlassReason,
        DateTimeOffset at,
        CancellationToken cancellationToken);
}
