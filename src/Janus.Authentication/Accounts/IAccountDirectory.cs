using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Authentication.Accounts;

/// <summary>
/// What the account operations ask of the account directory: the standing of the
/// account, its profile and its preferences.
/// </summary>
/// <remarks>
/// Implements REG-ACCT-001, REG-PROF-001, REG-PREF-001 and CONV-LAYOUT-001. What asks
/// here has already decided that the operation is allowed and that the deployment
/// collects the field, so a command that arrives is carried out. The one exception is
/// the preference set, whose rules belong to the declaration and are therefore applied
/// on the other side of the port.
/// </remarks>
internal interface IAccountDirectory
{
    /// <summary>
    /// Where an account stands.
    /// </summary>
    /// <param name="subject">Whose account.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The state, or nothing where no account bears the subject.</returns>
    ValueTask<AccountState?> StateAsync(SubjectId subject, CancellationToken cancellationToken);

    /// <summary>
    /// Who suspended an account, where it is suspended: what a self-deactivated
    /// account is reversed by is not what an administratively suspended one is
    /// (IDN-LIFE-013).
    /// </summary>
    /// <param name="subject">Whose account.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>Who suspended it, or nothing where it is not suspended.</returns>
    ValueTask<SuspensionOrigin?> SuspendedByAsync(
        SubjectId subject,
        CancellationToken cancellationToken);

    /// <summary>
    /// Stands a suspended account back up: a self-deactivated one from its link or at
    /// the completion of recovery (D-140), an administratively suspended one by an
    /// administrator. A restriction in force when it was suspended is in force again
    /// (IDN-LIFE-013).
    /// </summary>
    /// <param name="subject">Whose account.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of standing it up.</returns>
    ValueTask ReinstateAsync(SubjectId subject, CancellationToken cancellationToken);

    /// <summary>
    /// Suspends an account as an administrator, which is the <c>administrator</c> entry
    /// into the suspended state; an account its owner deactivated stays suspended and
    /// becomes the administrator's to reactivate.
    /// </summary>
    /// <param name="subject">Whose account.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of suspending it.</returns>
    ValueTask SuspendAsync(SubjectId subject, CancellationToken cancellationToken);

    /// <summary>
    /// The deletion an account is in the grace window of, where it is in one.
    /// </summary>
    /// <param name="subject">Whose account.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The deletion, or nothing where the account is not deleting.</returns>
    ValueTask<HeldDeletion?> DeletingAsync(SubjectId subject, CancellationToken cancellationToken);

    /// <summary>
    /// Takes an account down at its own request, which is the <c>self</c> entry into
    /// the suspended state.
    /// </summary>
    /// <param name="subject">Whose account.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of taking it down.</returns>
    ValueTask DeactivateAsync(SubjectId subject, CancellationToken cancellationToken);

    /// <summary>
    /// Begins the deletion grace window at the account's own request.
    /// </summary>
    /// <param name="subject">Whose account.</param>
    /// <param name="at">When the window began.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of beginning it.</returns>
    ValueTask BeginDeletionAsync(
        SubjectId subject,
        DateTimeOffset at,
        CancellationToken cancellationToken);

    /// <summary>
    /// Ends a grace window without erasing, restoring the account exactly as it
    /// stood.
    /// </summary>
    /// <param name="subject">Whose account.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of restoring it.</returns>
    ValueTask CancelDeletionAsync(SubjectId subject, CancellationToken cancellationToken);

    /// <summary>
    /// When an account came into being, which decides whether a raised requirement
    /// gives it a run-up or holds it at once (AUTH-FACT-017).
    /// </summary>
    /// <param name="subject">Whose account.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The instant, or nothing where no account bears the subject.</returns>
    ValueTask<DateTimeOffset?> CreatedAtAsync(SubjectId subject, CancellationToken cancellationToken);

    /// <summary>
    /// One account's profile as it stands.
    /// </summary>
    /// <param name="subject">Whose profile.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The profile, empty where the account has filled nothing in.</returns>
    ValueTask<HeldProfile> ProfileAsync(SubjectId subject, CancellationToken cancellationToken);

    /// <summary>
    /// Carries the two names the person edits onto the profile, clearing either where
    /// the edit gave it up. The date of birth is not here: the person never sets it.
    /// </summary>
    /// <param name="subject">Whose profile.</param>
    /// <param name="displayName">The display name, or nothing to clear it.</param>
    /// <param name="legalName">The legal name, or nothing to clear it.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of recording it.</returns>
    ValueTask RecordProfileAsync(
        SubjectId subject,
        DisplayName? displayName,
        LegalName? legalName,
        CancellationToken cancellationToken);

    /// <summary>
    /// The image an account shows for itself.
    /// </summary>
    /// <param name="subject">Whose photo.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// The stored JPEG, empty where the account shows none and where erasure has put
    /// the image out of reach (PRIV-RIGHT-005 AC4).
    /// </returns>
    ValueTask<ReadOnlyMemory<byte>> PhotoAsync(SubjectId subject, CancellationToken cancellationToken);

    /// <summary>
    /// Carries the image an account shows onto its photo, replacing what it showed.
    /// What arrives here is what the codec answered, so it is stored as it is.
    /// </summary>
    /// <param name="subject">Whose photo.</param>
    /// <param name="image">The re-encoded image.</param>
    /// <param name="at">When it was uploaded.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of recording it.</returns>
    ValueTask RecordPhotoAsync(
        SubjectId subject,
        ReadOnlyMemory<byte> image,
        DateTimeOffset at,
        CancellationToken cancellationToken);

    /// <summary>
    /// Gives up the image an account shows, which an account that shows none is
    /// unchanged by.
    /// </summary>
    /// <param name="subject">Whose photo.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of giving it up.</returns>
    ValueTask RemovePhotoAsync(SubjectId subject, CancellationToken cancellationToken);

    /// <summary>
    /// One account's preferences as they stand, carrying only what the account set.
    /// </summary>
    /// <param name="subject">Whose preferences.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The preferences.</returns>
    ValueTask<HeldPreferences> PreferencesAsync(
        SubjectId subject,
        CancellationToken cancellationToken);

    /// <summary>
    /// Replaces the preference set, validating every value against the key's
    /// declaration. A key the caller left out is given up, except one only an
    /// administrator may edit, which a person's replace leaves exactly as it stands.
    /// </summary>
    /// <param name="subject">Whose preferences.</param>
    /// <param name="language">The BCP 47 tag, or nothing to clear it.</param>
    /// <param name="timeZone">The IANA zone identifier, or nothing to clear it.</param>
    /// <param name="declared">The values to hold, by the key they were declared under.</param>
    /// <param name="asAdministrator">Whether an administrator is setting them.</param>
    /// <param name="maximumSize">What <c>preferences.maxsize</c> allows the set.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>Success, or the first refusal and its code.</returns>
    ValueTask<Result> RecordPreferencesAsync(
        SubjectId subject,
        string? language,
        string? timeZone,
        IReadOnlyDictionary<string, string> declared,
        bool asAdministrator,
        int maximumSize,
        CancellationToken cancellationToken);
}
