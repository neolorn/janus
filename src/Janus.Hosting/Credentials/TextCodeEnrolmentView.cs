using System;
using Janus.Core;

namespace Janus.Hosting.Credentials;

/// <summary>
/// The answer to a confirmed enrolment of a text code: the credential as
/// <c>GET /account/credentials</c> lists it, and the recovery codes it brought.
/// </summary>
/// <param name="Id">Which credential, as the other endpoints name it.</param>
/// <param name="Kind">Which catalogue entry it is an instance of.</param>
/// <param name="Label">What the person calls it.</param>
/// <param name="State">Where it stands.</param>
/// <param name="InvalidatesAt">When a suspended one is invalidated.</param>
/// <param name="BackupEligible">Whether a key may be backed up, where it holds one.</param>
/// <param name="BackupState">Whether a key is backed up, where it holds one.</param>
/// <param name="AddedAt">When it was enrolled.</param>
/// <param name="LastUsedAt">When it was last presented.</param>
/// <param name="RecoveryCodes">
/// The set the enrolment generated, shown this once, and nothing where it brought none
/// (AUTH-RECOV-006).
/// </param>
/// <remarks>Implements AUTH-FACT-002 and AUTH-RECOV-006.</remarks>
internal sealed record TextCodeEnrolmentView(
    string Id,
    Factor Kind,
    string Label,
    AuthenticatorState State,
    DateTimeOffset? InvalidatesAt,
    bool? BackupEligible,
    bool? BackupState,
    DateTimeOffset AddedAt,
    DateTimeOffset? LastUsedAt,
    RecoveryCodesView? RecoveryCodes)
{
    /// <summary>
    /// The answer for one confirmed enrolment.
    /// </summary>
    /// <param name="enrolled">What the confirmation left.</param>
    /// <returns>The answer.</returns>
    /// <exception cref="ArgumentNullException">The enrolment is absent.</exception>
    public static TextCodeEnrolmentView Of(TextCodeEnrolment enrolled)
    {
        ArgumentNullException.ThrowIfNull(enrolled);

        return new TextCodeEnrolmentView(
            enrolled.Credential.Id.ToString(),
            enrolled.Credential.Kind,
            enrolled.Credential.Label,
            enrolled.Credential.State,
            enrolled.Credential.InvalidatesAt,
            enrolled.Credential.BackupEligible,
            enrolled.Credential.BackupState,
            enrolled.Credential.AddedAt,
            enrolled.Credential.LastUsedAt,
            enrolled.RecoveryCodes is null ? null : RecoveryCodesView.Of(enrolled.RecoveryCodes));
    }
}
