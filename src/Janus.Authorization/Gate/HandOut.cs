using System.Collections.Generic;
using Janus.Core;

namespace Janus.Authorization.Gate;

/// <summary>
/// A change that hands out roles: a grant made or revoked, an invitation's roles, a change
/// of a group's members or the end of a membership.
/// </summary>
/// <param name="Operation">The permission the change is admitted under, which a refusal is recorded under.</param>
/// <param name="Organization">The organization the change is made in.</param>
/// <param name="Resource">The record the change is scoped to, or nothing for the whole organization.</param>
/// <param name="Roles">Every role the change touches.</param>
/// <param name="Widened">
/// The holders whose access the change widens: an allow grant's holder, a revoked deny's
/// holder, a member added to a group, a member taken out of a group that reaches a deny.
/// </param>
/// <remarks>Implements AUTHZ-GRANT-005 (D-212).</remarks>
internal sealed record HandOut(
    Permission Operation,
    OrganizationId Organization,
    ResourceReference? Resource,
    IReadOnlyList<DefinedRole> Roles,
    IReadOnlyList<GrantSubject> Widened);
