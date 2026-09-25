namespace Janus.Core;

/// <summary>
/// A code generator waiting for its first code: what the authenticator app is given,
/// and the credential it will become.
/// </summary>
/// <param name="Credential">Which credential, to confirm against.</param>
/// <param name="Secret">The shared secret in Base32, shown as text beside the code.</param>
/// <param name="Address">
/// The <c>otpauth</c> address the app is given, carrying the parameters of
/// AUTH-FACT-005.
/// </param>
/// <remarks>
/// Implements AUTH-FACT-005, AUTH-FACT-007 and chapter 18 FE-PM-006. The enrolment
/// does not become usable until one code of this secret is presented.
/// </remarks>
public sealed record GeneratorEnrolment(
    AuthenticatorId Credential,
    [property: NeverLogged] string Secret,
    [property: NeverLogged] string Address);
