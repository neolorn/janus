using System.Threading;
using System.Threading.Tasks;

namespace Janus.Core;

/// <summary>
/// The credentials an account holds: setting the password, enrolling a key or a code
/// generator, upgrading a second-factor key, generating recovery codes and removing
/// what the person still has.
/// </summary>
/// <remarks>
/// Implements LIB-API-005, AUTH-FACT-001, AUTH-FACT-002b, AUTH-FACT-007,
/// AUTH-FACT-008, AUTH-STEP-007, AUTH-RECOV-001 and AUTH-RECOV-006. Removing a
/// credential the person no longer holds is not here: that is a loss report
/// (<see cref="IRecovery.ReportLossAsync"/>), which asks for no gate because the
/// person has lost what the gate would ask them for.
/// </remarks>
public interface ICredentials
{
    /// <summary>
    /// Sets or changes the account's password. The floor applied is the one the
    /// account's reachable assurance asks for now (AUTH-PASS-001a).
    /// </summary>
    /// <param name="authority">What the request arrived under.</param>
    /// <param name="password">The password, which the caller clears after the call.</param>
    /// <param name="source">The address the request came from.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// Success, or <c>auth.stepup.required</c>, <c>auth.password.tooshort</c> or
    /// <c>auth.password.blocklisted</c>.
    /// </returns>
    ValueTask<Result> SetPasswordAsync(
        CredentialAuthority authority,
        [NeverLogged] string password,
        string source,
        CancellationToken cancellationToken);

    /// <summary>
    /// Opens a creation ceremony for a passkey or a second-factor security key.
    /// </summary>
    /// <param name="authority">What the request arrived under.</param>
    /// <param name="kind">Which catalogue entry the ceremony creates.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// What the browser is asked for, or <c>auth.stepup.required</c>,
    /// <c>auth.factor.notpermitted</c> or <c>auth.factor.rejected</c>.
    /// </returns>
    ValueTask<Result<CredentialCeremony>> BeginKeyAsync(
        CredentialAuthority authority,
        Factor kind,
        CancellationToken cancellationToken);

    /// <summary>
    /// Records what a creation ceremony produced, under the label the person gave it.
    /// </summary>
    /// <param name="authority">What the request arrived under.</param>
    /// <param name="attestation">What the browser sent back.</param>
    /// <param name="label">What the person calls it.</param>
    /// <param name="source">The address the request came from.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// The credential and what is now asked of the account, or
    /// <c>auth.credential.labelinvalid</c>, <c>auth.factor.rejected</c> or what the
    /// ceremony checks refused.
    /// </returns>
    ValueTask<Result<EnrolledCredential>> CompleteKeyAsync(
        CredentialAuthority authority,
        AuthenticatorAttestation attestation,
        string label,
        string source,
        CancellationToken cancellationToken);

    /// <summary>
    /// Opens the ceremony that replaces a second-factor security key with a passkey
    /// on the same hardware (AUTH-FACT-002b).
    /// </summary>
    /// <param name="authority">What the request arrived under.</param>
    /// <param name="credential">The second-factor entry being upgraded.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// What the browser is asked for, or <c>auth.stepup.required</c> or
    /// <c>auth.factor.rejected</c> where the entry is not a second-factor key.
    /// </returns>
    ValueTask<Result<CredentialCeremony>> UpgradeKeyAsync(
        CredentialAuthority authority,
        AuthenticatorId credential,
        CancellationToken cancellationToken);

    /// <summary>
    /// Begins a code generator's enrolment, which one valid code completes.
    /// </summary>
    /// <param name="authority">What the request arrived under.</param>
    /// <param name="label">What the person calls it.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// What the authenticator app is given, or <c>auth.stepup.required</c>,
    /// <c>auth.credential.labelinvalid</c> or <c>auth.factor.notpermitted</c> where
    /// the account holds no password.
    /// </returns>
    ValueTask<Result<GeneratorEnrolment>> BeginGeneratorAsync(
        CredentialAuthority authority,
        string label,
        CancellationToken cancellationToken);

    /// <summary>
    /// Completes a code generator's enrolment with one code of its secret.
    /// </summary>
    /// <param name="authority">What the request arrived under.</param>
    /// <param name="credential">Which enrolment.</param>
    /// <param name="code">What was typed.</param>
    /// <param name="source">The address the request came from.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// The credential and what is now asked of the account, or
    /// <c>auth.code.invalid</c>.
    /// </returns>
    ValueTask<Result<EnrolledCredential>> ConfirmGeneratorAsync(
        CredentialAuthority authority,
        AuthenticatorId credential,
        [NeverLogged] string code,
        string source,
        CancellationToken cancellationToken);

    /// <summary>
    /// Generates or regenerates the recovery-code set, which is returned once and
    /// never read back (AUTH-FACT-008, AUTH-FACT-009).
    /// </summary>
    /// <param name="authority">What the request arrived under.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// The codes, or <c>auth.stepup.required</c>, or
    /// <c>auth.factor.notpermitted</c> where the account holds no password
    /// (AUTH-RECOV-006).
    /// </returns>
    ValueTask<Result<GeneratedRecoveryCodes>> GenerateRecoveryCodesAsync(
        CredentialAuthority authority,
        CancellationToken cancellationToken);

    /// <summary>
    /// Removes a credential the person still holds. Where removing it would lower the
    /// account's reachable assurance, it is suspended now and invalidated when the
    /// notified window ends (AUTH-RECOV-007).
    /// </summary>
    /// <param name="authority">What the request arrived under.</param>
    /// <param name="credential">Which credential.</param>
    /// <param name="source">The address the request came from.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// Nothing where the credential is gone already, or
    /// <c>auth.credential.lastsecondfactor</c> carrying <c>invalidatesAt</c> where the
    /// window was opened instead, or <c>auth.stepup.required</c> or
    /// <c>auth.credential.notfound</c>.
    /// </returns>
    ValueTask<Result> RemoveAsync(
        CredentialAuthority authority,
        AuthenticatorId credential,
        string source,
        CancellationToken cancellationToken);
}
