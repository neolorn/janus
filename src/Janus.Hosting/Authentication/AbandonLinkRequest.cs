namespace Janus.Hosting.Authentication;

/// <summary>
/// The link a person ended from the browser it was opened in.
/// </summary>
/// <param name="LinkToken">The token the message carried.</param>
/// <remarks>Implements REG-SESS-003 and AUTH-FACT-003.</remarks>
internal sealed record AbandonLinkRequest(string? LinkToken);
