namespace Janus.Core;

/// <summary>
/// What confirming the enrolment of a text code leaves: the credential as the
/// account now lists it, and the recovery codes it brought with it.
/// </summary>
/// <param name="Credential">The credential the code made active.</param>
/// <param name="RecoveryCodes">
/// The set generated because a second step was enrolled beside a password, returned
/// once and never read back, and nothing where the enrolment brought none
/// (AUTH-RECOV-006).
/// </param>
/// <remarks>Implements AUTH-FACT-002 and AUTH-RECOV-006.</remarks>
public sealed record TextCodeEnrolment(
    CredentialSummary Credential,
    GeneratedRecoveryCodes? RecoveryCodes);
