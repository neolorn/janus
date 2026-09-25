using System.Text.Json.Serialization;

namespace Janus.Core;

/// <summary>
/// What a message the library sends is for. The words are never the library's: a
/// deployment's catalogue holds the text for each of these in each of its languages,
/// and the library states only which message it needs delivered.
/// </summary>
/// <remarks>Implements CONV-CONTENT-001, LIB-EXT-001, INT-SMS-001, INT-SMS-005a.</remarks>
public enum MessageKind
{
    /// <summary>
    /// A code that proves control of an address or a number.
    /// </summary>
    [JsonStringEnumMemberName("verification-code")]
    VerificationCode = 0,

    /// <summary>
    /// A link that signs the person in.
    /// </summary>
    [JsonStringEnumMemberName("signin-link")]
    SignInLink = 1,

    /// <summary>
    /// A code presented as a second step.
    /// </summary>
    [JsonStringEnumMemberName("secondstep-code")]
    SecondStepCode = 2,

    /// <summary>
    /// A notice that something happened to the account.
    /// </summary>
    [JsonStringEnumMemberName("security-notice")]
    SecurityNotice = 3,

    /// <summary>
    /// A link that carries an admin-assisted enrolment.
    /// </summary>
    [JsonStringEnumMemberName("enrolment-link")]
    EnrolmentLink = 4,

    /// <summary>
    /// A condition the operator has to see.
    /// </summary>
    [JsonStringEnumMemberName("alert")]
    Alert = 5,

    /// <summary>
    /// The answer to a request made for an address no account holds.
    /// </summary>
    [JsonStringEnumMemberName("no-account")]
    NoAccount = 6,

    /// <summary>
    /// The answer to a registration or a change made with an address an account
    /// already holds, sent to the holder and never to the person who tried.
    /// </summary>
    [JsonStringEnumMemberName("account-exists")]
    AccountExists = 7,

    /// <summary>
    /// An identifier was added to the account.
    /// </summary>
    [JsonStringEnumMemberName("identifier-added")]
    IdentifierAdded = 8,

    /// <summary>
    /// An identifier was removed, sent to the members of the security-notice set that
    /// remain and carrying the link that undoes it.
    /// </summary>
    [JsonStringEnumMemberName("identifier-removed")]
    IdentifierRemoved = 9,

    /// <summary>
    /// The identifier that was removed no longer reaches the account. It carries no
    /// link and no powers.
    /// </summary>
    [JsonStringEnumMemberName("identifier-detached")]
    IdentifierDetached = 10,

    /// <summary>
    /// The primary identifier of a kind, or the kind's backup setting, changed.
    /// </summary>
    [JsonStringEnumMemberName("identifier-settings-changed")]
    IdentifierSettingsChanged = 11,

    /// <summary>
    /// A credential was enrolled on the account.
    /// </summary>
    [JsonStringEnumMemberName("credential-enrolled")]
    CredentialEnrolled = 12,

    /// <summary>
    /// The address being displaced by a change is asked to confirm it, which is asked
    /// only where the account has no other channel at all.
    /// </summary>
    [JsonStringEnumMemberName("identifier-change-confirm")]
    IdentifierChangeConfirm = 13,

    /// <summary>
    /// The link a person asked for to set a new password, which restores nothing else
    /// and removes no factor.
    /// </summary>
    [JsonStringEnumMemberName("recovery-link")]
    RecoveryLink = 14,

    /// <summary>
    /// The automatic receipt a data subject request gets the moment it enters the
    /// queue, which is not a decision and starts nothing (PRIV-RIGHT-002).
    /// </summary>
    [JsonStringEnumMemberName("privacy-request-received")]
    PrivacyRequestReceived = 15,

    /// <summary>
    /// The honest word to a subject whose out-of-band erasure request reached its
    /// deadline undecided (PRIV-RIGHT-002).
    /// </summary>
    [JsonStringEnumMemberName("privacy-request-lapsed")]
    PrivacyRequestLapsed = 16,

    /// <summary>
    /// The word to an account that has just deactivated itself, carrying the link
    /// that stands it back up (IDN-LIFE-013).
    /// </summary>
    [JsonStringEnumMemberName("deactivation-notice")]
    DeactivationNotice = 17,

    /// <summary>
    /// The word to an account whose deletion grace window has begun, carrying the
    /// link that cancels it where the deletion is the account's own (IDN-LIFE-014).
    /// </summary>
    [JsonStringEnumMemberName("deletion-notice")]
    DeletionNotice = 18,

    /// <summary>
    /// The link an invitation into an organization carries, sent to the email the
    /// invitation binds (IDN-LIFE-009a, REG-MAIL-001).
    /// </summary>
    [JsonStringEnumMemberName("invitation-link")]
    InvitationLink = 19,
}
