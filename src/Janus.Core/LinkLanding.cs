namespace Janus.Core;

/// <summary>
/// What a verification link's landing page is told when it is opened rather than
/// pressed, or pressed from a browser that did not start the flow.
/// </summary>
/// <param name="SameBrowser">
/// Whether the request carries the cookie of the session that sent the link.
/// </param>
/// <param name="Code">
/// The code to type where the flow is waiting, present only where the browser is not
/// the originating one.
/// </param>
/// <remarks>
/// Implements REG-SESS-003, BFF-CSRF-005b and API-LAND-001. The server decides which
/// case applies; the frontend never guesses it from the user agent.
/// </remarks>
public sealed record LinkLanding(bool SameBrowser, string? Code);
