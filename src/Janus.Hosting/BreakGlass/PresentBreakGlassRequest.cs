using Janus.Core;

namespace Janus.Hosting.BreakGlass;

/// <summary>
/// The body of <c>POST /auth/break-glass</c>.
/// </summary>
/// <param name="Credential">The sealed code, as it was typed or scanned.</param>
/// <remarks>Implements OPS-BOOT-002 and FE-BG-001.</remarks>
[NeverLogged]
internal sealed record PresentBreakGlassRequest([property: NeverLogged] string? Credential);
