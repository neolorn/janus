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
    // The three actions that read by their name alone; every other action modifies
    // unless the host declared it reading (AUTHZ-GATE-006, D-160).
    private static readonly string[] Reading = ["read", "list", "export"];

    private readonly Dictionary<string, LawfulBasisDeclaration> _bases;
    private readonly Dictionary<Type, ResourceTypeDeclaration> _entities;
    private readonly HashSet<Permission> _permissions;
    private readonly HashSet<string> _readingActions;
    private readonly Dictionary<string, RelationshipDeclaration> _relationships;
    private readonly DeclaredProcessing _processing;
    private readonly IReadOnlyList<string> _sensitiveCategories;
    private readonly IReadOnlyDictionary<Permission, string> _stepUpGates;
    private readonly IReadOnlyDictionary<Permission, string> _actionPurposes;
    private readonly Dictionary<ResourceType, ResourceTypeDeclaration> _types;

    private AuthorizationModel(
        Dictionary<ResourceType, ResourceTypeDeclaration> types,
        Dictionary<Type, ResourceTypeDeclaration> entities,
        Dictionary<string, RelationshipDeclaration> relationships,
        HashSet<Permission> permissions,
        HashSet<string> readingActions,
        IReadOnlyDictionary<Permission, string> stepUpGates,
        IReadOnlyDictionary<Permission, string> actionPurposes,
        Dictionary<string, LawfulBasisDeclaration> bases,
        IReadOnlyList<string> sensitiveCategories,
        DeclaredProcessing processing)
    {
        _types = types;
        _entities = entities;
        _relationships = relationships;
        _permissions = permissions;
        _readingActions = readingActions;
        _stepUpGates = stepUpGates;
        _actionPurposes = actionPurposes;
        _bases = bases;
        _sensitiveCategories = sensitiveCategories;
        _processing = processing;
    }

    /// <summary>
    /// What the deployment processes, one entry per purpose.
    /// </summary>
    public DeclaredProcessing Processing => _processing;

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
        var categories = new HashSet<string>(declaration.SensitiveCategories, StringComparer.Ordinal);
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
            Check(type, types, relationships, bases, categories);

            if (!entities.TryAdd(type.Entity, type))
            {
                throw Malformed("the type " + type.Entity.Name + " is declared as two resource types");
            }
        }

        var processing = DeclaredProcessing.Of(declaration);

        return new AuthorizationModel(
            types,
            entities,
            relationships,
            Permissions(declaration),
            new HashSet<string>([.. Reading, .. declaration.ReadingActions], StringComparer.Ordinal),
            declaration.StepUpGates,
            Purposes(declaration, processing),
            bases,
            declaration.SensitiveCategories,
            processing);
    }

    /// <summary>
    /// Whether the model declares the permission, the library's own included.
    /// </summary>
    /// <param name="permission">The permission a role would grant.</param>
    /// <returns>Whether it is declared.</returns>
    public bool Declares(Permission permission) => _permissions.Contains(permission);

    /// <summary>
    /// Whether the permission's action reads rather than modifies, which is what a
    /// processing restriction is read against.
    /// </summary>
    /// <param name="permission">The permission being asked for.</param>
    /// <returns>Whether its action reads.</returns>
    /// <remarks>
    /// Implements AUTHZ-GATE-006 and D-160. An action nobody classified modifies, so a
    /// restriction refuses it.
    /// </remarks>
    public bool IsReading(Permission permission) => _readingActions.Contains(permission.Action);

    /// <summary>
    /// The step-up gate the action is bound to, or nothing where it is bound to none.
    /// </summary>
    /// <param name="permission">The permission being asked for.</param>
    /// <returns>The gate's name, or nothing.</returns>
    /// <remarks>
    /// Implements AUTHZ-GATE-005 and D-160. The gate is what a capability's
    /// <c>stepup</c> residual is read from; the three values it stands for are the
    /// principal's policy's.
    /// </remarks>
    public string? GateOf(Permission permission) =>
        _stepUpGates.TryGetValue(permission, out string? gate) ? gate : null;

    /// <summary>
    /// The purpose the action is done for, or nothing where it names none.
    /// </summary>
    /// <param name="permission">The permission being asked for.</param>
    /// <returns>The purpose, or nothing.</returns>
    /// <remarks>
    /// Implements PRIV-SENS-002 and PRIV-SENS-002a. Consent gates purposes and not
    /// records, so an action on a record carrying several purposes is refused only
    /// for the one it is done for.
    /// </remarks>
    public string? PurposeOf(Permission permission) =>
        _actionPurposes.TryGetValue(permission, out string? purpose) ? purpose : null;

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
    /// Every derivation reaching records of the type: those declared on the type
    /// itself and those declared on a type containing it, each with the relationship
    /// it follows from.
    /// </summary>
    /// <param name="type">The resource type.</param>
    /// <returns>The derivations, which is nothing where the type declares none.</returns>
    /// <remarks>
    /// Implements AUTHZ-DERIVE-001 and AUTHZ-DERIVE-002. A relationship on a container
    /// reaches what the container holds, as a grant on it does.
    /// </remarks>
    public IReadOnlyList<ReachingDerivation> Derivations(ResourceType type)
    {
        var reaching = new List<ReachingDerivation>();

        foreach (ResourceType at in Containment(type))
        {
            foreach (DerivationDeclaration derivation in _types[at].Derivations)
            {
                reaching.Add(new ReachingDerivation(
                    derivation,
                    _relationships[derivation.Relationship]));
            }
        }

        return reaching;
    }

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
                [.. _readingActions.Order(StringComparer.Ordinal)],
                [.. _stepUpGates
                    .Select(binding => new SerializedModel.StepUpGate(
                        binding.Key.ToString(), binding.Value))
                    .OrderBy(binding => binding.Permission, StringComparer.Ordinal)],
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
                .Select(purpose => new SerializedModel.Purpose(
                    purpose.Name,
                    purpose.Basis,
                    purpose.Assessment,
                    [.. purpose.DataCategories.Order(StringComparer.Ordinal)],
                    [.. purpose.SubjectCategories.Order(StringComparer.Ordinal)]))],
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
            relationship.Relation,
            relationship.HolderColumn,
            relationship.ResourceColumn);

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

    // AUTHZ-MODEL-004: an action bound to a purpose no type declares would leave
    // the gate asking about a consent nobody can give, so the binding is checked at
    // startup and not at the first request that would have been refused.
    private static IReadOnlyDictionary<Permission, string> Purposes(
        AuthorizationDeclaration declaration,
        DeclaredProcessing processing)
    {
        foreach ((Permission permission, string purpose) in declaration.ActionPurposes)
        {
            if (processing.Find(purpose) is null)
            {
                throw Refused(
                    ErrorCodes.StartupUndeclaredTypeReference,
                    "permission",
                    permission.ToString(),
                    "it is bound to the purpose " + purpose + ", which no resource type declares");
            }
        }

        return declaration.ActionPurposes;
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
        Dictionary<string, LawfulBasisDeclaration> bases,
        IReadOnlyCollection<string> categories)
    {
        CheckContainment(type, types);
        CheckOrganizationPath(type, types);
        CheckSensitivity(type, categories);
        CheckPurposes(type, bases);
        CheckDerivations(type, relationships);
    }

    private static void CheckSensitivity(
        ResourceTypeDeclaration type,
        IReadOnlyCollection<string> categories)
    {
        foreach (string category in type.SensitiveCategories)
        {
            if (!categories.Contains(category))
            {
                throw Malformed(
                    "the type " + type.Name + " is declared sensitive in " + category
                    + ", which the model does not declare as a sensitivity category");
            }
        }
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

            if (purpose.DataCategories.Count == 0)
            {
                throw Refused(
                    ErrorCodes.StartupDeclarationMissing,
                    "key",
                    purpose.Name,
                    "a purpose is declared with the categories of data it requires");
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

    // What a refusal of the model reads as, wherever it is raised: the value at fault,
    // why it is at fault, and the code of `10` section 1.5 that names the condition.
    internal static StartupException Refused(ErrorCode code, string name, string value, string why) =>
        new(
            "The authorization model is refused: " + value + ", because " + why + ".",
            Error.From(code, name, JsonSerializer.SerializeToElement(value)));

    private static StartupException Malformed(string why) =>
        new("The authorization model is refused, because " + why + ".");
}
