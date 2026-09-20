namespace Janus.Authentication.SignIn;

/// <summary>
/// What opening a sign-in link produced, with what only the browser boundary can act
/// on.
/// </summary>
/// <param name="Outcome">
/// The sign-in and its secrets, where the press came from the browser that asked for
/// the link, and nothing otherwise.
/// </param>
/// <param name="SameBrowser">Whether the request came from that browser.</param>
/// <param name="Code">
/// The code to type where the sign-in began, present only where it did not.
/// </param>
/// <remarks>
/// Implements AUTH-FACT-003 and REG-SESS-003. The contract method is this one without
/// the secrets, because a caller in process has no cookie to write them to.
/// </remarks>
internal sealed record LandedSignIn(SignInOutcome? Outcome, bool SameBrowser, string? Code);
