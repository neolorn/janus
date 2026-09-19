namespace Janus.Authentication.Passwords;

/// <summary>
/// What a password that verified says beyond having verified: whether the person is
/// to be asked to change it before they go on.
/// </summary>
/// <param name="ChangeRequired">
/// Whether the password now matches something the deployment rejects on, which
/// happens when a profile field is set to the password after the password was
/// (AUTH-PASS-004).
/// </param>
/// <remarks>
/// Implements AUTH-PASS-004 and AUTH-PASS-007. A prompt is never a refusal: the
/// sign-in completes either way.
/// </remarks>
internal sealed record PasswordVerification(bool ChangeRequired);
