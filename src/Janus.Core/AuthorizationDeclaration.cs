using System.Collections.Generic;

namespace Janus.Core;

/// <summary>
/// Everything a host declares about its own domain: its resource types and their
/// containment, the permissions its roles may grant, the lawful bases its purposes
/// may rest on, and the sensitivity categories its types may carry.
/// </summary>
/// <param name="ResourceTypes">The kinds of thing the host holds.</param>
/// <param name="Relationships">The facts in the host's data a derivation may follow from.</param>
/// <param name="Permissions">The permissions the host declares, beside the library's own.</param>
/// <param name="LawfulBases">The closed list a purpose's basis is drawn from.</param>
/// <param name="SensitiveCategories">The closed list a type's sensitivity is drawn from.</param>
/// <remarks>
/// Implements AUTHZ-MODEL-001 and AUTHZ-MODEL-006. It is what
/// <see cref="AuthorizationDeclarationBuilder"/> produces and what the model is built
/// and validated from at startup; nothing changes it afterwards.
/// </remarks>
public sealed record AuthorizationDeclaration(
    IReadOnlyList<ResourceTypeDeclaration> ResourceTypes,
    IReadOnlyList<RelationshipDeclaration> Relationships,
    IReadOnlyList<Permission> Permissions,
    IReadOnlyList<LawfulBasisDeclaration> LawfulBases,
    IReadOnlyList<string> SensitiveCategories);
