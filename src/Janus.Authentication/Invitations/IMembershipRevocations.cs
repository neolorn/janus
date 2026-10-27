using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Authentication.Invitations;

/// <summary>
/// The grants an ended membership revokes and the groups it takes the account out of.
/// </summary>
/// <remarks>
/// Implements IDN-MEM-001, AUTHZ-GRANT-005 and CONV-LAYOUT-001 (D-213, D-217). The grants
/// and groups are the authorization area's; this is what ending a membership asks of them.
/// The end reaches the account's live allow grants a person gave in the organization, its
/// deny grants standing, and the organization's groups naming the account; for the
/// administrative organization, the same in every organization the account holds no
/// current membership of.
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
    /// <param name="actor">Who ended it.</param>
    /// <param name="reason">Why.</param>
    /// <param name="breakGlassReason">
    /// The reason given at the use of the break-glass credential, where the end was made
    /// in the session it opened, or nothing.
    /// </param>
    /// <param name="at">When.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// The events announcing each change made, which the end publishes in its transaction
    /// (chapter 10 section 5b, D-218).
    /// </returns>
    /// <exception cref="InvalidOperationException">No transaction is open.</exception>
    ValueTask<IReadOnlyList<DomainEvent>> RevokeAsync(
        SubjectId member,
        OrganizationId organization,
        bool administrative,
        SubjectId actor,
        string reason,
        string? breakGlassReason,
        DateTimeOffset at,
        CancellationToken cancellationToken);

    /// <summary>
    /// Revokes the grants an end set in advance reaches at its instant and takes the
    /// account out of the groups it reaches, inside the transaction the job's pass runs in;
    /// each grant carries the nil revoker and each record is the job's, naming the
    /// administrator who set the end (IDN-MEM-001, D-217).
    /// </summary>
    /// <param name="member">Whose membership ended.</param>
    /// <param name="organization">Of which organization.</param>
    /// <param name="administrative">Whether the organization administers the deployment.</param>
    /// <param name="principal">The principal carrying the end out.</param>
    /// <param name="endSetBy">The administrator who set the end.</param>
    /// <param name="reason">The reason the end was set with.</param>
    /// <param name="at">When.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// The events announcing each change made, which the end publishes in its transaction
    /// (chapter 10 section 5b, D-218).
    /// </returns>
    /// <exception cref="InvalidOperationException">No transaction is open.</exception>
    ValueTask<IReadOnlyList<DomainEvent>> RevokeAsync(
        SubjectId member,
        OrganizationId organization,
        bool administrative,
        SystemPrincipal principal,
        SubjectId endSetBy,
        string reason,
        DateTimeOffset at,
        CancellationToken cancellationToken);
}
