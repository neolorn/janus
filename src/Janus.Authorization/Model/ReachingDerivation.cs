using Janus.Core;

namespace Janus.Authorization.Model;

/// <summary>
/// One derivation reaching records of a resource type, with the relationship it
/// follows from: declared on the type itself, or on a type containing it.
/// </summary>
/// <param name="Derivation">What the relationship confers.</param>
/// <param name="Relationship">The fact in the host's data it follows from.</param>
/// <remarks>
/// Implements AUTHZ-DERIVE-001 and AUTHZ-DERIVE-002: inheritance applies to a derived
/// grant as it applies to a stored one, so a relationship on a container reaches what
/// the container holds.
/// </remarks>
internal sealed record ReachingDerivation(
    DerivationDeclaration Derivation,
    RelationshipDeclaration Relationship);
