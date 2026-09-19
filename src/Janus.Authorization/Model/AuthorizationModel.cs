using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Janus.Core;

namespace Janus.Authorization.Model;

/// <summary>
/// The host's declaration, checked and indexed for the gate to read. Building it is
/// where a declaration that could never behave correctly stops the deployment, rather
/// than the first query that meets it.
/// </summary>
/// <remarks>
/// Implements AUTHZ-MODEL-001 to AUTHZ-MODEL-004 and AUTHZ-MODEL-006. Nothing changes
/// it after it is built: roles and grants are runtime data, the model is not.
/// </remarks>
internal sealed class AuthorizationModel
{
    private readonly Dictionary<string, LawfulBasisDeclaration> _bases;
    private readonly Dictionary<Type, ResourceTypeDeclaration> _entities;
    private readonly HashSet<Permission> _permissions;
    private readonly Dictionary<string, RelationshipDeclaration> _relationships;
    private readonly IReadOnlyList<string> _sensitiveCategories;
    private readonly Dictionary<ResourceType, ResourceTypeDeclaration> _types;

    private AuthorizationModel(
        Dictionary<ResourceType, ResourceTypeDeclaration> types,
        Dictionary<Type, ResourceTypeDeclaration> entities,
        Dictionary<string, RelationshipDeclaration> relationships,
        HashSet<Permission> permissions,
        Dictionary<string, LawfulBasisDeclaration> bases,
        IReadOnlyList<string> sensitiveCategories)
    {
        _types = types;
        _entities = entities;
        _relationships = relationships;
        _permissions = permissions;
        _bases = bases;
        _sensitiveCategories = sensitiveCategories;
    }

    /// <summary>
    /// Every resource type the host declared.
    /// </summary>
    public IReadOnlyCollection<ResourceTypeDeclaration> ResourceTypes => _types.Values;

    /// <summary>
    /// Every relationship a derivation may follow from.
    /// </summary>
    public IReadOnlyCollection<RelationshipDeclaration> Relationships => _relationships.Values;

    /// <summary>
    /// Builds the model from what the host declared, refusing a declaration that
    /// could not behave correctly.
    /// </summary>
    /// <param name="declaration">What the host declared.</param>
    /// <returns>The model.</returns>
    /// <exception cref="ArgumentNullException">The declaration is absent.</exception>
    /// <exception cref="StartupException">The declaration is one of those AUTHZ-MODEL-004 refuses.</exception>
    public static AuthorizationModel Of(AuthorizationDeclaration declaration)
    {
        ArgumentNullException.ThrowIfNull(declaration);

        Dictionary<string, LawfulBasisDeclaration> bases = Bases(declaration);
        Dictionary<ResourceType, ResourceTypeDeclaration> types = Types(declaration);
        var entities = new Dictionary<Type, ResourceTypeDeclaration>();
        var relationships = new Dictionary<string, RelationshipDeclaration>(StringComparer.Ordinal);

        foreach (RelationshipDeclaration relationship in declaration.Relationships)
        {
            if (!types.ContainsKey(relationship.On))
            {
                throw Refused(
                    ErrorCodes.StartupUndeclaredDerivationReference,
                    "relationship",
                    relationship.Name,
                    "it is about a resource type the model does not declare");
            }

            if (!relationships.TryAdd(relationship.Name, relationship))
            {
                throw Malformed("the relationship " + relationship.Name + " is declared twice");
            }
        }

        foreach (ResourceTypeDeclaration type in types.Values)
        {
            Check(type, types, relationships, bases);

            if (!entities.TryAdd(type.Entity, type))
            {
                throw Malformed("the type " + type.Entity.Name + " is declared as two resource types");
            }
        }

        return new AuthorizationModel(
            types,
            entities,
            relationships,
            Permissions(declaration),
            bases,
            declaration.SensitiveCategories);
    }

    /// <summary>
    /// Whether the model declares the permission, the library's own included.
    /// </summary>
    /// <param name="permission">The permission a role would grant.</param>
    /// <returns>Whether it is declared.</returns>
    public bool Declares(Permission permission) => _permissions.Contains(permission);

    /// <summary>
    /// The declaration of a resource type, or nothing where the model declares none.
    /// </summary>
    /// <param name="type">The resource type.</param>
    /// <returns>Its declaration, or nothing.</returns>
    public ResourceTypeDeclaration? Find(ResourceType type) =>
        _types.TryGetValue(type, out ResourceTypeDeclaration? declaration) ? declaration : null;

    /// <summary>
    /// The declaration registered for one of the host's own types, or nothing where
    /// none is, which is the unregistered policy of AUTHZ-GATE-001.
    /// </summary>
    /// <param name="entity">The host's own type.</param>
    /// <returns>Its declaration, or nothing.</returns>
    public ResourceTypeDeclaration? Find(Type entity) =>
        _entities.TryGetValue(entity, out ResourceTypeDeclaration? declaration) ? declaration : null;

    /// <summary>
    /// The declaration of a relationship, or nothing where the model declares none.
    /// </summary>
    /// <param name="name">The relationship.</param>
    /// <returns>Its declaration, or nothing.</returns>
    public RelationshipDeclaration? Relationship(string name) =>
        _relationships.TryGetValue(name, out RelationshipDeclaration? declaration) ? declaration : null;

    /// <summary>
    /// The resource type and every type containing it, outermost last. Inheritance is
    /// read from this and from nothing else.
    /// </summary>
    /// <param name="type">The resource type.</param>
    /// <returns>The type and its containers, or nothing where the type is undeclared.</returns>
    public IReadOnlyList<ResourceType> Containment(ResourceType type)
    {
        var chain = new List<ResourceType>();

        for (ResourceType? at = type; at is not null;)
        {
            if (!_types.TryGetValue(at.Value, out ResourceTypeDeclaration? declaration))
            {
                return chain;
            }

            chain.Add(at.Value);
            at = declaration.ContainedIn;
        }

        return chain;
    }

    /// <summary>
    /// Writes the built model beside the build's other outputs, so a change to what
    /// the host declared is a diff in review rather than a difference noticed later.
    /// </summary>
    /// <param name="directory">Where the file goes.</param>
    /// <returns>The path written.</returns>
    /// <exception cref="ArgumentException">The directory is absent or blank.</exception>
    public string WriteTo(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);

        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "model.json");
        File.WriteAllText(path, Serialize());

        return path;
    }

    /// <summary>
    /// The built model as it is committed: every list in one order, so two runs of
    /// one configuration produce the same bytes.
    /// </summary>
    /// <returns>The model as JSON.</returns>
    public string Serialize() =>
        JsonSerializer.Serialize(
            new SerializedModel(
                [.. _types.Values.OrderBy(type => type.Name.ToString(), StringComparer.Ordinal).Select(Serialized)],
                [.. _relationships.Values.OrderBy(relationship => relationship.Name, StringComparer.Ordinal).Select(Serialized)],
                [.. _permissions.Select(permission => permission.ToString()).Order(StringComparer.Ordinal)],
                [.. _bases.Values.OrderBy(basis => basis.Key, StringComparer.Ordinal).Select(Serialized)],
                [.. _sensitiveCategories.Order(StringComparer.Ordinal)]),
            ModelJson.Default.SerializedModel);

    private static SerializedModel.Type Serialized(ResourceTypeDeclaration type) =>
        new(
            type.Name.ToString(),
            type.ContainedIn?.ToString(),
            type.BelongsToOrganization,
            type.Concealment == ConcealmentBehaviour.Disclose ? "disclose" : "conceal",
            [.. type.SensitiveCategories.Order(StringComparer.Ordinal)],
            [.. type.Purposes
                .OrderBy(purpose => purpose.Name, StringComparer.Ordinal)
                .Select(purpose => new SerializedModel.Purpose(purpose.Name, purpose.Basis, purpose.Assessment))],
            [.. type.Derivations
                .OrderBy(derivation => derivation.Relationship, StringComparer.Ordinal)
                .Select(derivation => new SerializedModel.Derivation(
                    derivation.Relationship, derivation.Role.ToString(), derivation.Materialised))],
            [.. type.EncryptedFields
                .OrderBy(field => field.Field, StringComparer.Ordinal)
                .Select(field => new SerializedModel.Field(field.Field, field.SubjectColumn))]);

    private static SerializedModel.Relationship Serialized(RelationshipDeclaration relationship) =>
        new(
            relationship.Name,
            relationship.On.ToString(),
            relationship.Table,
            relationship.SubjectColumn,
            [.. relationship.Columns.Order(StringComparer.Ordinal)]);

    private static SerializedModel.Basis Serialized(LawfulBasisDeclaration basis) =>
        new(
            basis.Key,
            basis.IsConsent,
            basis.RequiresWrittenConsentForSensitive,
            basis.RequiresAssessment,
            basis.IsObjectable);

    private static Dictionary<ResourceType, ResourceTypeDeclaration> Types(
        AuthorizationDeclaration declaration)
    {
        var types = new Dictionary<ResourceType, ResourceTypeDeclaration>();

        foreach (ResourceTypeDeclaration type in declaration.ResourceTypes)
        {
            if (!types.TryAdd(type.Name, type))
            {
                throw Malformed("the resource type " + type.Name + " is declared twice");
            }
        }

        return types;
    }

    private static Dictionary<string, LawfulBasisDeclaration> Bases(
        AuthorizationDeclaration declaration)
    {
        var bases = new Dictionary<string, LawfulBasisDeclaration>(StringComparer.Ordinal);

        foreach (LawfulBasisDeclaration basis in declaration.LawfulBases)
        {
            if (!bases.TryAdd(basis.Key, basis))
            {
                throw Malformed("the lawful basis " + basis.Key + " is declared twice");
            }
        }

        return bases;
    }

    private static HashSet<Permission> Permissions(AuthorizationDeclaration declaration)
    {
        var permissions = new HashSet<Permission>(Core.Permissions.All);

        foreach (Permission permission in declaration.Permissions)
        {
            // Chapter 10 section 2.2: a host declares its own and redefines none of
            // the library's, so a collision is a declaration to fix rather than a
            // silent replacement.
            if (!permissions.Add(permission))
            {
                throw Malformed("the permission " + permission + " is declared twice");
            }
        }

        return permissions;
    }

    private static void Check(
        ResourceTypeDeclaration type,
        Dictionary<ResourceType, ResourceTypeDeclaration> types,
        Dictionary<string, RelationshipDeclaration> relationships,
        Dictionary<string, LawfulBasisDeclaration> bases)
    {
        CheckContainment(type, types);
        CheckOrganizationPath(type, types);
        CheckPurposes(type, bases);
        CheckDerivations(type, relationships);
    }

    private static void CheckContainment(
        ResourceTypeDeclaration type,
        Dictionary<ResourceType, ResourceTypeDeclaration> types)
    {
        var seen = new HashSet<ResourceType> { type.Name };

        for (ResourceType? at = type.ContainedIn; at is not null;)
        {
            if (!types.TryGetValue(at.Value, out ResourceTypeDeclaration? container))
            {
                throw Refused(
                    ErrorCodes.StartupUndeclaredTypeReference,
                    "type",
                    type.Name.ToString(),
                    "it is contained in " + at.Value + ", which the model does not declare");
            }

            if (!seen.Add(at.Value))
            {
                throw Refused(
                    ErrorCodes.StartupContainmentCycle,
                    "type",
                    type.Name.ToString(),
                    "its containment reaches itself");
            }

            at = container.ContainedIn;
        }
    }

    private static void CheckOrganizationPath(
        ResourceTypeDeclaration type,
        Dictionary<ResourceType, ResourceTypeDeclaration> types)
    {
        for (ResourceTypeDeclaration? at = type; at is not null;)
        {
            if (at.BelongsToOrganization)
            {
                return;
            }

            at = at.ContainedIn is { } container ? types[container] : null;
        }

        throw Refused(
            ErrorCodes.StartupNoOrganizationPath,
            "type",
            type.Name.ToString(),
            "neither it nor anything containing it names the organization owning it");
    }

    private static void CheckPurposes(
        ResourceTypeDeclaration type,
        Dictionary<string, LawfulBasisDeclaration> bases)
    {
        if (type.Purposes.Count == 0)
        {
            throw Refused(
                ErrorCodes.StartupDeclarationMissing,
                "key",
                type.Name.ToString(),
                "a resource type is declared with what it is processed for");
        }

        foreach (PurposeDeclaration purpose in type.Purposes)
        {
            if (!bases.TryGetValue(purpose.Basis, out LawfulBasisDeclaration? basis))
            {
                throw Malformed(
                    "the purpose " + purpose.Name + " rests on " + purpose.Basis
                    + ", which the model does not declare as a lawful basis");
            }

            if (basis.RequiresAssessment && string.IsNullOrWhiteSpace(purpose.Assessment))
            {
                throw Refused(
                    ErrorCodes.StartupMissingAssessment,
                    "purpose",
                    purpose.Name,
                    "its basis requires an assessment and it names none");
            }
        }
    }

    private static void CheckDerivations(
        ResourceTypeDeclaration type,
        Dictionary<string, RelationshipDeclaration> relationships)
    {
        foreach (DerivationDeclaration derivation in type.Derivations)
        {
            if (!relationships.ContainsKey(derivation.Relationship))
            {
                throw Refused(
                    ErrorCodes.StartupUndeclaredDerivationReference,
                    "type",
                    type.Name.ToString(),
                    "it derives from " + derivation.Relationship
                    + ", which the model does not declare as a relationship");
            }
        }
    }

    private static StartupException Refused(ErrorCode code, string name, string value, string why) =>
        new(
            "The authorization model is refused: " + value + ", because " + why + ".",
            Error.From(code, name, JsonSerializer.SerializeToElement(value)));

    private static StartupException Malformed(string why) =>
        new("The authorization model is refused, because " + why + ".");
}
