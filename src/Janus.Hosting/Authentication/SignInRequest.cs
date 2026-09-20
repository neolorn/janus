namespace Janus.Hosting.Authentication;

/// <summary>
/// One identifier, as the person entered it.
/// </summary>
/// <param name="Identifier">The email, the phone or, where enabled, the username.</param>
/// <remarks>
/// Implements AUTH-FACT-002 and AUTH-ABUSE-003. The kind is detected from the value,
/// so nothing here says which kind was meant.
/// </remarks>
internal sealed record SignInRequest(string? Identifier);
