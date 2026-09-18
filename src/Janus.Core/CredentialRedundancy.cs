using System.Text.Json.Serialization;

namespace Janus.Core;

/// <summary>
/// Whether a second credential is required after a device-bound enrolment, or only
/// offered.
/// </summary>
/// <remarks>Implements chapter 10 section 4.1a, AUTH-RECOV-001.</remarks>
public enum CredentialRedundancy
{
    /// <summary>
    /// The person is offered a second credential and may decline it.
    /// </summary>
    [JsonStringEnumMemberName("advisory")]
    Advisory = 0,

    /// <summary>
    /// The person holds a second credential before the enrolment completes.
    /// </summary>
    [JsonStringEnumMemberName("enforced")]
    Enforced = 1,
}
