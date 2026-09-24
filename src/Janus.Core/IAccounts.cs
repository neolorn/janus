using System;
using System.Threading;
using System.Threading.Tasks;

namespace Janus.Core;

/// <summary>
/// The standing of other people's accounts, changed under <c>account:manage</c> in the
/// administrative organization.
/// </summary>
/// <remarks>
/// Implements LIB-API-005, IDN-LIFE-003, IDN-LIFE-013, AUTH-SESS-010, PRIV-RIGHT-004
/// and chapter 09 section 8a. Suspension and reactivation are the
/// <c>account:suspend</c> and <c>account:reactivate</c> step-up actions; lifting a
/// restriction and cancelling a deletion are none. Suspension ends every session of the
/// account in the transaction that suspends it, and reactivation restores what the
/// account held exactly as it held it, a restriction in force included. Every change is
/// audited as the administrator's, on the account it was made on; reading the photo
/// changes nothing and is not.
/// </remarks>
public interface IAccounts
{
    /// <summary>
    /// Suspends an account as an administrator, which only an administrator reverses.
    /// An account its owner deactivated stays suspended and can no longer be stood
    /// back up by its owner; one already suspended by an administrator changes nothing.
    /// </summary>
    /// <param name="context">Who is asking.</param>
    /// <param name="session">The session the step-up is judged on.</param>
    /// <param name="subject">Whose account.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// Success, or the refusal: <c>api.request.malformed</c> naming <c>subject</c>
    /// where no account bears it, <c>authz.denied</c> where the account is being
    /// deleted or has been.
    /// </returns>
    ValueTask<Result> SuspendAsync(
        AccessContext context,
        SessionId session,
        SubjectId subject,
        CancellationToken cancellationToken);

    /// <summary>
    /// Reactivates an account an administrator suspended.
    /// </summary>
    /// <param name="context">Who is asking.</param>
    /// <param name="session">The session the step-up is judged on.</param>
    /// <param name="subject">Whose account.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// Success, or the refusal: <c>api.request.malformed</c> naming <c>subject</c>
    /// where no account bears it, <c>authz.denied</c> where an administrator has not
    /// suspended it.
    /// </returns>
    ValueTask<Result> ReactivateAsync(
        AccessContext context,
        SessionId session,
        SubjectId subject,
        CancellationToken cancellationToken);

    /// <summary>
    /// Lifts a restriction of processing, which restores what the account did before it
    /// exactly and tells every subject-event handler.
    /// </summary>
    /// <param name="context">Who is asking.</param>
    /// <param name="subject">Whose account.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// Success, or the refusal: <c>api.request.malformed</c> naming <c>subject</c>
    /// where no account bears it, <c>authz.denied</c> where the account is not
    /// restricted, including one that holds a restriction while suspended or deleting.
    /// </returns>
    ValueTask<Result> LiftRestrictionAsync(
        AccessContext context,
        SubjectId subject,
        CancellationToken cancellationToken);

    /// <summary>
    /// Cancels a deletion inside its grace window on the subject's behalf, which restores
    /// the account as it stood. A window an out-of-band erasure request began is recorded
    /// against that request.
    /// </summary>
    /// <param name="context">Who is asking.</param>
    /// <param name="subject">Whose account.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// Success, or the refusal: <c>identity.takedown.active</c> where a takedown began
    /// the window, <c>identity.deletion.windowelapsed</c> where it has closed,
    /// <c>api.request.malformed</c> naming <c>subject</c> where no account bears it,
    /// <c>authz.denied</c> where the account is not in a grace window.
    /// </returns>
    ValueTask<Result> CancelDeletionAsync(
        AccessContext context,
        SubjectId subject,
        CancellationToken cancellationToken);

    /// <summary>
    /// The profile photo an account shows, for the management application.
    /// </summary>
    /// <param name="context">Who is asking.</param>
    /// <param name="subject">Whose account.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// The stored JPEG, empty where the account shows none or no organization it belongs
    /// to shows photos; or the refusal: <c>api.request.malformed</c> naming
    /// <c>subject</c> where no account bears it.
    /// </returns>
    ValueTask<Result<ReadOnlyMemory<byte>>> ReadPhotoAsync(
        AccessContext context,
        SubjectId subject,
        CancellationToken cancellationToken);
}
