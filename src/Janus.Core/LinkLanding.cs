namespace Janus.Core;

/// <summary>
/// What a verification link did: verified, or, where the press did not come from the
/// browser that started the flow, nothing at all and the code to type instead.
/// </summary>
/// <param name="Verified">
/// Whether the press completed the verification, which only a press from the
/// originating browser does.
/// </param>
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
public sealed record LinkLanding(bool Verified, bool SameBrowser, string? Code);
