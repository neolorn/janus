using System.Collections.Generic;

namespace Janus.Core;

/// <summary>
/// What a role carries and hands out once its inclusions are followed, each entry with
/// where it came from.
/// </summary>
/// <param name="Permissions">
/// Every permission a grant of the role admits, its own and those its inclusions pass on,
/// each listed once, by name.
/// </param>
/// <param name="HandsOut">
/// Every role a holder of the role may hand out by it, its own and those its inclusions
/// pass on, each listed once, by name.
/// </param>
/// <remarks>Implements AUTHZ-GRANT-006 (D-278).</remarks>
public sealed record EffectiveRole(
    IReadOnlyList<EffectiveEntry<Permission>> Permissions,
    IReadOnlyList<EffectiveEntry<RoleName>> HandsOut);
