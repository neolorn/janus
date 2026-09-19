using System.Text.Json.Serialization;

namespace Janus.Core;

/// <summary>
/// A policy field whose raising gives the accounts under it a run-up to comply.
/// </summary>
/// <remarks>
/// Implements chapter 10 section 5, AUTH-FACT-017. These are the two fields a raise
/// can leave an account short of; every other field takes effect at once.
/// </remarks>
public enum PolicyField
{
    /// <summary>
    /// The assurance floor a sign-in has to reach.
    /// </summary>
    [JsonStringEnumMemberName("requiredAssurance")]
    RequiredAssurance = 0,

    /// <summary>
    /// Whether a second credential is asked for or required.
    /// </summary>
    [JsonStringEnumMemberName("credentialRedundancy")]
    CredentialRedundancy = 1,
}
