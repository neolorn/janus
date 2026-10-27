using System;
using System.Threading;
using System.Threading.Tasks;

namespace Janus.Core;

/// <summary>
/// The standing of other people's accounts, changed under <c>account:manage</c> in the
/// administrative organization, and what a host reads of them.
/// </summary>
/// <remarks>
/// Implements LIB-API-005, IDN-LIFE-003, IDN-LIFE-013, AUTH-SESS-010, PRIV-RIGHT-004
/// and chapter 09 section 8a. Suspension, reactivation, lifting a restriction and
/// cancelling a deletion are the <c>account:suspend</c>, <c>account:reactivate</c>,
/// <c>account:restrictionlift</c> and <c>account:deletioncancel</c> step-up actions, each
/// judged after every other refusal. Suspension ends every session of the
/// account in the transaction that suspends it, and reactivation restores what the
/// account held exactly as it held it, a restriction in force included. Every change is
/// audited as the administrator's, on the account it was made on; reading the photo
/// changes nothing and is not. Implements IDN-READ-003 and IDN-READ-004 for the reads:
/// each is asked in the organization named, the administrative organization where none
/// is, and reaches every account from the administrative organization and elsewhere only
/// the organization's current members and the data subjects of its records. An account
/// beyond that reach is answered as an account no person bears, and each account a
/// person reads counts toward their read volume. A read is not audited.
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
    /// Success, or the refusal: <c>identity.account.notfound</c> where no account bears
    /// the subject, <c>identity.account.stateconflict</c> where the account is being
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
    /// Success, or the refusal: <c>identity.account.notfound</c> where no account bears
    /// the subject, <c>identity.account.stateconflict</c> where an administrator has not
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
    /// <param name="session">The session the step-up is judged on.</param>
    /// <param name="subject">Whose account.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// Success, or the refusal: <c>identity.account.notfound</c> where no account bears
    /// the subject, <c>identity.account.stateconflict</c> where the account is not
    /// restricted, including one that holds a restriction while suspended or deleting.
    /// </returns>
    ValueTask<Result> LiftRestrictionAsync(
        AccessContext context,
        SessionId session,
        SubjectId subject,
        CancellationToken cancellationToken);

    /// <summary>
    /// Cancels a deletion inside its grace window on the subject's behalf, which restores
    /// the account as it stood. A window an out-of-band erasure request began is recorded
    /// against that request.
    /// </summary>
    /// <param name="context">Who is asking.</param>
    /// <param name="session">The session the step-up is judged on.</param>
    /// <param name="subject">Whose account.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// Success, or the refusal: <c>identity.account.notfound</c> where no account bears
    /// the subject, <c>identity.account.stateconflict</c> where the account is not in a
    /// grace window, <c>identity.takedown.active</c> where a takedown began the window,
    /// <c>identity.deletion.windowelapsed</c> where it has closed.
    /// </returns>
    ValueTask<Result> CancelDeletionAsync(
        AccessContext context,
        SessionId session,
        SubjectId subject,
        CancellationToken cancellationToken);

    /// <summary>
    /// The profile photo an account shows, for the management application.
    /// </summary>
    /// <param name="context">Who is asking.</param>
    /// <param name="subject">Whose account.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// The stored JPEG, or the refusal: <c>identity.account.notfound</c> where no account
    /// bears the subject, <c>identity.photo.notfound</c> where the account shows none or
    /// no organization it belongs to shows photos, alike.
    /// </returns>
    ValueTask<Result<ReadOnlyMemory<byte>>> ReadPhotoAsync(
        AccessContext context,
        SubjectId subject,
        CancellationToken cancellationToken);

    /// <summary>
    /// Another person's account, under <c>account:read</c>.
    /// </summary>
    /// <param name="context">Who is asking.</param>
    /// <param name="subject">Whose account.</param>
    /// <param name="organization">
    /// The organization the read is asked in, the administrative organization where
    /// nothing is named.
    /// </param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// The account, or the refusal: <c>identity.organization.notfound</c> where the
    /// deployment holds no such organization or erased it, <c>authz.denied</c> where the
    /// caller does not hold the permission there, <c>identity.account.notfound</c> where
    /// no account bears the subject, the account is deleted or it is beyond the reach.
    /// </returns>
    ValueTask<Result<AccountView>> ReadAsync(
        AccessContext context,
        SubjectId subject,
        OrganizationId? organization,
        CancellationToken cancellationToken);

    /// <summary>
    /// The account bearing an email address, a phone number or a username, read in the
    /// canonical form sign-in reads it in, under <c>account:read</c>.
    /// </summary>
    /// <param name="context">Who is asking.</param>
    /// <param name="identifier">The identifier, as entered.</param>
    /// <param name="organization">
    /// The organization the finding is asked in, the administrative organization where
    /// nothing is named.
    /// </param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// The account's subject, or the refusal as <see cref="ReadAsync"/> answers it, an
    /// identifier no account bears answered <c>identity.account.notfound</c>.
    /// </returns>
    ValueTask<Result<SubjectId>> FindAsync(
        AccessContext context,
        string identifier,
        OrganizationId? organization,
        CancellationToken cancellationToken);

    /// <summary>
    /// What a host needs to write to another person, under <c>contact:read</c>, which a
    /// host's declared job may hold.
    /// </summary>
    /// <param name="context">Who is asking.</param>
    /// <param name="subject">Whose account.</param>
    /// <param name="organization">
    /// The organization the read is asked in, the administrative organization where
    /// nothing is named.
    /// </param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The contact details, or the refusal as <see cref="ReadAsync"/> answers it.</returns>
    ValueTask<Result<ContactDetail>> ContactAsync(
        AccessContext context,
        SubjectId subject,
        OrganizationId? organization,
        CancellationToken cancellationToken);
}
