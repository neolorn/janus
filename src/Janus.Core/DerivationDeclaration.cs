namespace Janus.Core;

/// <summary>
/// One derivation: whoever holds the named relationship in the host's own data holds
/// the named role on the resource type this is declared on. There is nothing to keep
/// in sync, because there is no row.
/// </summary>
/// <param name="Relationship">The declared relationship it follows from.</param>
/// <param name="Role">The role whoever holds the relationship holds.</param>
/// <param name="Materialised">
/// Whether the derivation is precomputed into grant rows rather than evaluated per
/// request, which is the last rung of the optimisation ladder.
/// </param>
/// <remarks>
/// Implements AUTHZ-DERIVE-001, AUTHZ-DERIVE-003 and AUTHZ-DERIVE-005.
/// </remarks>
public sealed record DerivationDeclaration(string Relationship, RoleName Role, bool Materialised);
