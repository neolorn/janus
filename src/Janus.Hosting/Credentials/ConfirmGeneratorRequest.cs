namespace Janus.Hosting.Credentials;

/// <summary>
/// The one code that confirms a generator's enrolment.
/// </summary>
/// <param name="CredentialId">Which enrolment.</param>
/// <param name="Code">What was typed.</param>
/// <remarks>Implements AUTH-FACT-007.</remarks>
internal sealed record ConfirmGeneratorRequest(string? CredentialId, string? Code);
