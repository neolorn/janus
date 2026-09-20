namespace Janus.Core;

/// <summary>
/// What opening a sign-in link produced.
/// </summary>
/// <param name="SignedIn">
/// What the sign-in reached, where the press came from the browser that requested the
/// link, and nothing otherwise.
/// </param>
/// <param name="SameBrowser">
/// Whether the request came from the browser that asked for the link.
/// </param>
/// <param name="Code">
/// The code to type where the sign-in began, present only where the browser is not
/// the one that asked.
/// </param>
/// <remarks>
/// Implements AUTH-FACT-003, REG-SESS-003 and API-LAND-001. A plain open changes
/// nothing: the link completes on a press and in one browser, and the landing
/// elsewhere signs nothing in. The server decides which case applies; the frontend
/// never guesses it.
/// </remarks>
public sealed record SignInLanding(SignInProgress? SignedIn, bool SameBrowser, string? Code);
