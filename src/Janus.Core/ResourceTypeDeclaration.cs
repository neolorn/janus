using System;
using System.Collections.Generic;

namespace Janus.Core;

/// <summary>
/// One kind of thing a host holds, as the host declares it. One declaration is what
/// permission filtering, error semantics and the records of processing all derive
/// from.
/// </summary>
/// <param name="Name">The name the host chose.</param>
/// <param name="Entity">The host's own type, which the gate renders its filter over.</param>
/// <param name="ContainedIn">
/// The type that contains it, whose grants it inherits, or nothing where it is
/// contained in none.
/// </param>
/// <param name="BelongsToOrganization">
/// Whether a record of this type names the organization owning it, rather than
/// reaching one through its container.
/// </param>
/// <param name="Concealment">What a denial on one record discloses.</param>
/// <param name="SensitiveCategories">The declared categories its data falls in.</param>
/// <param name="Purposes">What it is processed for, and on which lawful basis.</param>
/// <param name="Derivations">The relationships in the host's data that confer a role on it.</param>
/// <param name="EncryptedFields">Its encrypted fields, each with the column naming its subject.</param>
/// <remarks>Implements AUTHZ-MODEL-001, AUTHZ-MODEL-002, AUTHZ-MODEL-003.</remarks>
public sealed record ResourceTypeDeclaration(
    ResourceType Name,
    Type Entity,
    ResourceType? ContainedIn,
    bool BelongsToOrganization,
    ConcealmentBehaviour Concealment,
    IReadOnlyList<string> SensitiveCategories,
    IReadOnlyList<PurposeDeclaration> Purposes,
    IReadOnlyList<DerivationDeclaration> Derivations,
    IReadOnlyList<EncryptedFieldDeclaration> EncryptedFields);
