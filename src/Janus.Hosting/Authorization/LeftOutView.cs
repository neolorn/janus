using System.Collections.Generic;

namespace Janus.Hosting.Authorization;

/// <summary>
/// What an inclusion does not pass on, as the management application reads it.
/// </summary>
/// <param name="Permissions">The permissions it leaves out.</param>
/// <param name="HandsOut">The roles of the included role's <c>handsOut</c> it leaves out.</param>
/// <remarks>Implements chapter 09 section 8 and AUTHZ-GRANT-006 (D-278).</remarks>
internal sealed record LeftOutView(IReadOnlyList<string> Permissions, IReadOnlyList<string> HandsOut);
