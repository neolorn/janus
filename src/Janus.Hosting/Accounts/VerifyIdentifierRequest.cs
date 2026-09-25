using Janus.Core;

namespace Janus.Hosting.Accounts;

/// <summary>
/// One identifier proved, by the typed code or by the link pressed in the browser
/// that added it.
/// </summary>
/// <param name="Code">The code, where it was typed.</param>
/// <param name="LinkToken">The token of the link, where one was opened.</param>
/// <param name="Press">Whether the person pressed, rather than the page loading.</param>
/// <remarks>Implements REG-IDENT-004, REG-SESS-003 and API-LAND-001.</remarks>
internal sealed record VerifyIdentifierRequest(
    [property: NeverLogged] string? Code,
    [property: NeverLogged] string? LinkToken,
    bool Press);
