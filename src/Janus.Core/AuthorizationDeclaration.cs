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
/// <param name="ReadingActions">
/// The actions the host declares to be reading rather than modifying, beside the three
/// that are reading by name.
/// </param>
/// <param name="StepUpGates">
/// The step-up gate each of the host's actions is bound to, where it binds one.
/// </param>
/// <param name="ActionPurposes">
/// The purpose each of the host's actions serves, where it names one. Consent gates
/// purposes and not records (PRIV-SENS-002a), so what a consent is required for is
/// what the action is done for, and an action naming no purpose requires none.
/// </param>
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
    IReadOnlyList<string> ReadingActions,
    IReadOnlyDictionary<Permission, string> StepUpGates,
    IReadOnlyDictionary<Permission, string> ActionPurposes,
    IReadOnlyList<LawfulBasisDeclaration> LawfulBases,
    IReadOnlyList<string> SensitiveCategories);
