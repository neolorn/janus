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
}
