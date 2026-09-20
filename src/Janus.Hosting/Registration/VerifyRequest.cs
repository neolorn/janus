namespace Janus.Hosting.Registration;

/// <summary>
/// One identifier proved, by the code typed where the flow is waiting or by the link
/// pressed in the browser that staged it.
/// </summary>
/// <param name="Code">The code, where it was typed.</param>
/// <param name="LinkToken">The token of the link, where one was opened.</param>
/// <param name="Press">Whether the person pressed, rather than the page loading.</param>
/// <remarks>Implements REG-SESS-003 and API-LAND-001.</remarks>
internal sealed record VerifyRequest(string? Code, string? LinkToken, bool Press);
