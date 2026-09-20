namespace Janus.Authentication.Passwords;

/// <summary>
/// A password that has passed the floor and the screening, hashed and not yet stored.
/// </summary>
/// <param name="Hash">What it hashes to under the parameters now in force.</param>
/// <param name="StandsAlone">
/// Whether it reaches the single-factor floor, which is what decides whether a second
/// step is mandatory beside it.
/// </param>
/// <param name="Feedback">The advice to show beside the password that was accepted.</param>
/// <remarks>
/// Implements AUTH-PASS-001a and REG-SESS-006. Registration stages one of these
/// because no account exists to store a password against until the terms step.
/// </remarks>
internal sealed record PreparedPassword(
    PasswordHash Hash,
    bool StandsAlone,
    PasswordFeedback Feedback);
