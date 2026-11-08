using System.Collections.Generic;

namespace Janus.Core;

/// <summary>
/// A role a person holds themselves, as they read it: what it carries and hands out once
/// its inclusions are followed, and the roles that may give it.
/// </summary>
/// <param name="Name">The role.</param>
/// <param name="Effective">What it carries and hands out, each entry with its source.</param>
/// <param name="GivenBy">
/// The roles whose effective <c>handsOut</c> names it, by name; none where no role does.
/// </param>
/// <remarks>Implements AUTHZ-GRANT-006 and chapter 09 <c>GET /account/roles</c> (D-280).</remarks>
public sealed record HeldRole(RoleName Name, EffectiveRole Effective, IReadOnlyList<RoleName> GivenBy);
