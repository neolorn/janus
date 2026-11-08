using System.Collections.Generic;

namespace Janus.Hosting.Authorization;

/// <summary>
/// What an inclusion is not to pass on, as the request reads.
/// </summary>
/// <param name="Permissions">The permissions it leaves out, or nothing for none.</param>
/// <param name="HandsOut">The roles of the included role's <c>handsOut</c> it leaves out, or nothing for none.</param>
/// <remarks>Implements chapter 09 section 8 and AUTHZ-GRANT-006 (D-278).</remarks>
internal sealed record LeftOutBody(IReadOnlyList<string?>? Permissions, IReadOnlyList<string?>? HandsOut);
