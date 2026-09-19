using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace Janus.Core;

/// <summary>
/// Declares one resource type. Every reference to one of the host's own fields is an
/// expression the compiler checks, so renaming a property is a build error rather
/// than a permission that quietly stops matching.
/// </summary>
/// <typeparam name="TResource">The host's own type.</typeparam>
/// <remarks>Implements AUTHZ-MODEL-001 and AUTHZ-MODEL-003.</remarks>
public sealed class ResourceTypeDeclarationBuilder<TResource>
{
    private readonly List<DerivationDeclaration> _derivations = [];
    private readonly List<EncryptedFieldDeclaration> _encrypted = [];
    private readonly List<PurposeDeclaration> _purposes = [];
    private readonly List<string> _sensitive = [];
    private readonly ResourceType _name;

    private bool _belongsToOrganization;
    private ConcealmentBehaviour _concealment = ConcealmentBehaviour.Conceal;
    private ResourceType? _containedIn;

    internal ResourceTypeDeclarationBuilder(ResourceType name) => _name = name;

    /// <summary>
    /// Declares the type that contains this one, whose grants it inherits at any
    /// depth.
    /// </summary>
    /// <param name="container">The containing type's name.</param>
    /// <returns>This builder.</returns>
    /// <exception cref="ArgumentException">The name is not a well-formed type name.</exception>
    public ResourceTypeDeclarationBuilder<TResource> ContainedIn(string container)
    {
        _containedIn = ResourceType.Parse(container);

        return this;
    }

    /// <summary>
    /// Declares that a record of this type names the organization owning it, which is
    /// how a permission evaluation is scoped without reading the session.
    /// </summary>
    /// <returns>This builder.</returns>
    public ResourceTypeDeclarationBuilder<TResource> BelongsToOrganization()
    {
        _belongsToOrganization = true;

        return this;
    }

    /// <summary>
    /// Declares that a denial on one record of this type says the record exists and
    /// is forbidden, rather than concealing it.
    /// </summary>
    /// <returns>This builder.</returns>
    public ResourceTypeDeclarationBuilder<TResource> Discloses()
    {
        _concealment = ConcealmentBehaviour.Disclose;

        return this;
    }

    /// <summary>
    /// Declares a sensitivity category this type's data falls in.
    /// </summary>
    /// <param name="category">The category, from the declared list.</param>
    /// <returns>This builder.</returns>
    /// <exception cref="ArgumentException">The category is absent or blank.</exception>
    public ResourceTypeDeclarationBuilder<TResource> Sensitive(string category)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(category);
        _sensitive.Add(category);

        return this;
    }

    /// <summary>
    /// Declares what this type is processed for and the lawful basis it rests on.
    /// </summary>
    /// <param name="name">The purpose.</param>
    /// <param name="basis">The key of the lawful basis it rests on.</param>
    /// <param name="assessment">
    /// The legitimate interest assessment, where the basis requires one.
    /// </param>
    /// <returns>This builder.</returns>
    /// <exception cref="ArgumentException">The purpose or the basis is absent or blank.</exception>
    public ResourceTypeDeclarationBuilder<TResource> Purpose(
        string name,
        string basis,
        string? assessment = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(basis);
        _purposes.Add(new PurposeDeclaration(name, basis, assessment));

        return this;
    }

    /// <summary>
    /// Declares a field held as ciphertext and the field naming the subject whose key
    /// encrypts it.
    /// </summary>
    /// <param name="field">The encrypted field.</param>
    /// <param name="subject">The field naming the subject.</param>
    /// <returns>This builder.</returns>
    /// <exception cref="ArgumentNullException">Either expression is absent.</exception>
    /// <exception cref="ArgumentException">Either expression names no member.</exception>
    public ResourceTypeDeclarationBuilder<TResource> Encrypted(
        Expression<Func<TResource, object?>> field,
        Expression<Func<TResource, object?>> subject)
    {
        _encrypted.Add(new EncryptedFieldDeclaration(DeclaredMember.Of(field), DeclaredMember.Of(subject)));

        return this;
    }

    /// <summary>
    /// Declares that whoever holds a declared relationship in the host's own data
    /// holds a role on this type, with no grant written and nothing to keep in sync.
    /// </summary>
    /// <param name="relationship">The declared relationship it follows from.</param>
    /// <param name="role">The role it confers.</param>
    /// <param name="materialised">
    /// Whether it is precomputed into grant rows rather than evaluated per request.
    /// </param>
    /// <returns>This builder.</returns>
    /// <exception cref="ArgumentException">
    /// The relationship is absent or blank, or the role is not a well-formed role
    /// name.
    /// </exception>
    public ResourceTypeDeclarationBuilder<TResource> Derivation(
        string relationship,
        string role,
        bool materialised = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relationship);
        _derivations.Add(new DerivationDeclaration(relationship, RoleName.Parse(role), materialised));

        return this;
    }

    internal ResourceTypeDeclaration Build() =>
        new(_name,
            typeof(TResource),
            _containedIn,
            _belongsToOrganization,
            _concealment,
            _sensitive,
            _purposes,
            _derivations,
            _encrypted);
}
