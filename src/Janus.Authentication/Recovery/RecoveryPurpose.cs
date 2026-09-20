using System.Text.Json.Serialization;

namespace Janus.Authentication.Recovery;

/// <summary>
/// What a recovery link is for, which decides the one endpoint that consumes it.
/// </summary>
/// <remarks>
/// Implements AUTH-RECOV-002, AUTH-RECOV-005 and D-147. The two links are the same
/// artefact and never interchangeable: a link issued to the person themselves cannot
/// open a re-enrolment session, and a link an approver issued cannot set a password
/// without one.
/// </remarks>
internal enum RecoveryPurpose
{
    /// <summary>
    /// Issued to the account holder by their own request, spent by completing
    /// recovery, and good for the password alone.
    /// </summary>
    [JsonStringEnumMemberName("self-service")]
    SelfService = 0,

    /// <summary>
    /// Issued by an approver after out-of-band confirmation, spent by opening the
    /// enrolment session.
    /// </summary>
    [JsonStringEnumMemberName("enrolment")]
    Enrolment = 1,
}
