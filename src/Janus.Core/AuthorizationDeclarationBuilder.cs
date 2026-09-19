using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace Janus.Core;

/// <summary>
/// Where a host declares its own domain to the library: its resource types and their
/// containment, the permissions its roles may grant, the lawful bases its purposes
/// rest on, and the sensitivity categories its types carry. The library ships no type
/// name belonging to any business, so two hosts with entirely different domains run
/// the same binary.
/// </summary>
/// <remarks>
/// Implements AUTHZ-MODEL-001, AUTHZ-MODEL-002 and AUTHZ-MODEL-006. The declaration
/// is read once, at startup; nothing at runtime changes it.
/// </remarks>
public sealed class AuthorizationDeclarationBuilder
{
    private readonly List<LawfulBasisDeclaration> _bases = [];
    private readonly List<Permission> _permissions = [];
    private readonly List<string> _readingActions = [];
    private readonly List<RelationshipDeclaration> _relationships = [];
    private readonly List<ResourceTypeDeclaration> _resourceTypes = [];
    private readonly List<string> _sensitiveCategories = [];
    private readonly Dictionary<Permission, string> _stepUpGates = [];

    /// <summary>
    /// Declares one of the host's kinds of thing.
    /// </summary>
    /// <typeparam name="TResource">The host's own type.</typeparam>
    /// <param name="name">The name the host chose for it.</param>
    /// <param name="declare">What else is true of it.</param>
    /// <returns>This builder.</returns>
    /// <exception cref="ArgumentNullException">The declaration is absent.</exception>
    /// <exception cref="ArgumentException">The name is not a well-formed type name.</exception>
    public AuthorizationDeclarationBuilder Resource<TResource>(
        string name,
        Action<ResourceTypeDeclarationBuilder<TResource>> declare)
    {
        ArgumentNullException.ThrowIfNull(declare);

        var builder = new ResourceTypeDeclarationBuilder<TResource>(ResourceType.Parse(name));
        declare(builder);
        _resourceTypes.Add(builder.Build());

        return this;
    }

    /// <summary>
    /// Declares a fact in the host's own data that a derivation may follow from: a
    /// column of one of the host's tables naming a subject.
    /// </summary>
    /// <typeparam name="TResource">The host's own type.</typeparam>
    /// <param name="name">The relationship, in the language of the host's domain.</param>
    /// <param name="on">The resource type the fact is about.</param>
    /// <param name="table">The host table holding it.</param>
    /// <param name="subject">The field of that table naming the subject.</param>
    /// <returns>This builder.</returns>
    /// <exception cref="ArgumentNullException">The subject expression is absent.</exception>
    /// <exception cref="ArgumentException">
    /// A name is absent or blank, the resource type is not a well-formed name, or the
    /// expression references no member.
    /// </exception>
    public AuthorizationDeclarationBuilder Relationship<TResource>(
        string name,
        string on,
        string table,
        Expression<Func<TResource, object?>> subject)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(table);

        string column = DeclaredMember.Of(subject);

        _relationships.Add(new RelationshipDeclaration(
            name, ResourceType.Parse(on), table, column, [column]));

        return this;
    }

    /// <summary>
    /// Declares a permission the host's own roles may grant.
    /// </summary>
    /// <param name="permission">The permission, as <c>resource:action</c>.</param>
    /// <returns>This builder.</returns>
    /// <exception cref="ArgumentException">The value is not a well-formed permission.</exception>
    public AuthorizationDeclarationBuilder Permission(string permission)
    {
        _permissions.Add(Core.Permission.Parse(permission));

        return this;
    }

    /// <summary>
    /// Declares a permission the host's own roles may grant, saying whether its action
    /// reads or modifies. An action named <c>read</c>, <c>list</c> or <c>export</c>
    /// reads without being declared; every other action modifies unless it is declared
    /// here, which is what a processing restriction is read against (AUTHZ-GATE-006).
    /// </summary>
    /// <param name="permission">The permission, as <c>resource:action</c>.</param>
    /// <param name="reading">Whether the action reads rather than modifies.</param>
    /// <returns>This builder.</returns>
    /// <exception cref="ArgumentException">The value is not a well-formed permission.</exception>
    public AuthorizationDeclarationBuilder Permission(string permission, bool reading)
    {
        var parsed = Core.Permission.Parse(permission);

        _permissions.Add(parsed);

        if (reading)
        {
            _readingActions.Add(parsed.Action);
        }

        return this;
    }

    /// <summary>
    /// Binds one of the host's actions to a step-up gate: what a session has to have
    /// proved, how recently, before the action is exercised. The gate is one of chapter
    /// 10 section 5a or one the host names itself, and the three values it stands for
    /// are set by the principal's policy, never here.
    /// </summary>
    /// <param name="permission">The permission, as <c>resource:action</c>.</param>
    /// <param name="gate">The gate's name.</param>
    /// <returns>This builder.</returns>
    /// <exception cref="ArgumentException">
    /// The permission is not well-formed, the gate is absent or blank, or the
    /// permission is already bound to a gate.
    /// </exception>
    public AuthorizationDeclarationBuilder StepUpGate(string permission, string gate)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(gate);

        var parsed = Core.Permission.Parse(permission);

        if (!_stepUpGates.TryAdd(parsed, gate))
        {
            throw new ArgumentException(
                "The permission " + parsed + " is bound to a step-up gate twice.",
                nameof(permission));
        }

        return this;
    }

    /// <summary>
    /// Declares a lawful basis a purpose may rest on.
    /// </summary>
    /// <param name="basis">The basis and the properties the library branches on.</param>
    /// <returns>This builder.</returns>
    /// <exception cref="ArgumentNullException">The basis is absent.</exception>
    public AuthorizationDeclarationBuilder LawfulBasis(LawfulBasisDeclaration basis)
    {
        ArgumentNullException.ThrowIfNull(basis);
        _bases.Add(basis);

        return this;
    }

    /// <summary>
    /// Declares a sensitivity category a resource type may carry.
    /// </summary>
    /// <param name="category">The category, which is a label nothing branches on.</param>
    /// <returns>This builder.</returns>
    /// <exception cref="ArgumentException">The category is absent or blank.</exception>
    public AuthorizationDeclarationBuilder SensitiveCategory(string category)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(category);
        _sensitiveCategories.Add(category);

        return this;
    }

    /// <summary>
    /// Closes the declaration.
    /// </summary>
    /// <returns>What the host declared.</returns>
    public AuthorizationDeclaration Build() =>
        new(
            [.. _resourceTypes],
            [.. _relationships],
            [.. _permissions],
            [.. _readingActions],
            new Dictionary<Permission, string>(_stepUpGates),
            [.. _bases],
            [.. _sensitiveCategories]);
}
