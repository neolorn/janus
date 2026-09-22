using System.Collections.Generic;
using Janus.Core;

namespace Janus.Authorization.Gate;

/// <summary>
/// One derivation reaching a resource type: the fact it follows from, the role it
/// confers, and what that role allows.
/// </summary>
/// <param name="Relationship">The fact in the host's data it follows from.</param>
/// <param name="Role">The role it confers.</param>
/// <param name="Confers">What that role allows, read where the grants are read.</param>
/// <remarks>
/// Implements AUTHZ-DERIVE-001, AUTHZ-DERIVE-002 and AUTHZ-GATE-005 AC1. What the role
/// allows is model data and is mapped in memory, so a page of records costs one query
/// over the host's rows and not one per permission.
/// </remarks>
internal sealed record ConferredDerivation(
    RelationshipDeclaration Relationship,
    RoleName Role,
    IReadOnlySet<Permission> Confers);
