using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using Janus.Core.Configuration;

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

    private readonly List<RecipientDeclaration> _recipients = [];
    private readonly Dictionary<Permission, string> _stepUpGates = [];
    private readonly Dictionary<Permission, string> _actionPurposes = [];
    private readonly Dictionary<string, TimeSpan> _retentionFloors = new(StringComparer.Ordinal);
    private readonly List<CookieDeclaration> _cookies = [];
    private readonly List<RoleDeclaration> _roles = [];
    private readonly List<JobDeclaration> _jobs = [];

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
    /// Declares a fact in the host's own data that a derivation may follow from: a row
    /// of one of the host's relations naming a subject and one of its records. The two
    /// selectors are read where the predicate is composed into the host's own query;
    /// the relation is what the SQL rendering names.
    /// </summary>
    /// <typeparam name="TRelationship">The host's own row.</typeparam>
    /// <param name="name">The relationship, in the language of the host's domain.</param>
    /// <param name="on">The resource type the fact is about.</param>
    /// <param name="relation">The relation holding it, as SQL names it.</param>
    /// <param name="holder">The field of that row naming the subject.</param>
    /// <param name="holderColumn">That field's column, as SQL names it.</param>
    /// <param name="resource">The field of that row naming the record.</param>
    /// <param name="resourceColumn">That field's column, as SQL names it.</param>
    /// <returns>This builder.</returns>
    /// <exception cref="ArgumentNullException">A selector is absent.</exception>
    /// <exception cref="ArgumentException">
    /// A name is absent or blank, or the resource type is not a well-formed name.
    /// </exception>
    public AuthorizationDeclarationBuilder Relationship<TRelationship>(
        string name,
        string on,
        string relation,
        Expression<Func<TRelationship, SubjectId>> holder,
        string holderColumn,
        Expression<Func<TRelationship, string>> resource,
        string resourceColumn)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(relation);
        ArgumentException.ThrowIfNullOrWhiteSpace(holderColumn);
        ArgumentException.ThrowIfNullOrWhiteSpace(resourceColumn);
        ArgumentNullException.ThrowIfNull(holder);
        ArgumentNullException.ThrowIfNull(resource);

        _relationships.Add(new RelationshipDeclaration(
            name,
            ResourceType.Parse(on),
            relation,
            holderColumn,
            resourceColumn,
            holder,
            resource));

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
    /// <remarks>
    /// Implements AUTHZ-GATE-005 (D-160, D-209). A permission of the library's own carries
    /// its gate in chapter 10 section 5a, so binding one is refused as the deployment
    /// starts.
    /// </remarks>
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
    /// Binds one of the host's actions to the purpose it is done for. Where that
    /// purpose rests on consent, the gate refuses the action until the subject has
    /// consented to it, and the capability says so rather than the control failing
    /// silently.
    /// </summary>
    /// <param name="permission">The permission, as <c>resource:action</c>.</param>
    /// <param name="purpose">The purpose, as a resource type declares it.</param>
    /// <returns>This builder.</returns>
    /// <exception cref="ArgumentException">
    /// The permission is not well-formed, the purpose is absent or blank, or the
    /// permission already serves a purpose.
    /// </exception>
    /// <remarks>
    /// Implements AUTHZ-GATE-005 (D-162, D-209). Only the host's own actions are bound to
    /// a purpose, so binding a permission of the library's own is refused as the
    /// deployment starts.
    /// </remarks>
    public AuthorizationDeclarationBuilder ServesPurpose(string permission, string purpose)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(purpose);

        var parsed = Core.Permission.Parse(permission);

        if (!_actionPurposes.TryAdd(parsed, purpose))
        {
            throw new ArgumentException(
                "The permission " + parsed + " is bound to a purpose twice.",
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
    /// Declares someone the deployment's personal data reaches.
    /// </summary>
    /// <param name="recipient">
    /// The recipient and the six columns the records of processing report for it.
    /// <see cref="ProviderRegister.Default"/> ships the rows of chapter 05 section 6,
    /// which a row declared under the same name replaces.
    /// </param>
    /// <returns>This builder.</returns>
    /// <exception cref="ArgumentNullException">The recipient is absent.</exception>
    public AuthorizationDeclarationBuilder Recipient(RecipientDeclaration recipient)
    {
        ArgumentNullException.ThrowIfNull(recipient);
        _recipients.Add(recipient);

        return this;
    }

    /// <summary>
    /// Declares the shortest period a category of data is kept. The category is kept
    /// for that period until the deployment states a longer one, and a stated period
    /// below it stops the deployment at startup.
    /// </summary>
    /// <param name="category">The category, as a purpose names it.</param>
    /// <param name="floor">The period.</param>
    /// <returns>This builder.</returns>
    /// <exception cref="ArgumentException">
    /// The category is absent, blank or cannot name a configuration key, or its floor
    /// is already declared.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">The period is not positive.</exception>
    /// <remarks>Implements PRIV-RET-001 and LIB-HOST-001.</remarks>
    public AuthorizationDeclarationBuilder RetentionFloor(string category, TimeSpan floor)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(category);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(floor, TimeSpan.Zero);
        _ = Settings.HostCategoryRetention.For(category);

        if (!_retentionFloors.TryAdd(category, floor))
        {
            throw new ArgumentException(
                "The category " + category + " is given a retention floor twice.",
                nameof(category));
        }

        return this;
    }

    /// <summary>
    /// Declares a cookie the host sets, which the privacy notice lists beside the
    /// library's own.
    /// </summary>
    /// <param name="name">What the cookie is called.</param>
    /// <param name="lifetime">How long it is set for, or null where it ends with the browser's session.</param>
    /// <param name="thirdPartyReadable">Whether any third party can read it.</param>
    /// <param name="purpose">
    /// The consent-based purpose it serves, or null where it is strictly necessary.
    /// </param>
    /// <returns>This builder.</returns>
    /// <exception cref="ArgumentNullException">The name is absent.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The lifetime is not positive.</exception>
    /// <remarks>
    /// Implements PRIV-CONS-006a and LIB-HOST-001 (D-203). A purpose not declared or not
    /// resting on consent, a name the library's own cookies take, a name that is no
    /// cookie name, or a cookie declared twice is refused as the deployment starts.
    /// </remarks>
    public AuthorizationDeclarationBuilder Cookie(
        string name,
        TimeSpan? lifetime,
        bool thirdPartyReadable,
        string? purpose)
    {
        ArgumentNullException.ThrowIfNull(name);

        if (lifetime is TimeSpan set)
        {
            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(set, TimeSpan.Zero, nameof(lifetime));
        }

        _cookies.Add(new CookieDeclaration(name, lifetime, thirdPartyReadable, purpose));

        return this;
    }

    /// <summary>
    /// Declares a role the deployment starts with. It is defined at the first start
    /// that finds it not yet seeded, where no role of its name exists, and is the
    /// administrators' to change or remove from then on; a later start never defines it
    /// again.
    /// </summary>
    /// <param name="name">
    /// The role's name, as <c>POST /admin/roles</c> takes one and never a role chapter
    /// 10 section 3 seeds.
    /// </param>
    /// <param name="permissions">
    /// What holding it permits, each as <c>resource:action</c>: permissions the host
    /// declares, or the library's own other than <c>system:administer</c>.
    /// </param>
    /// <returns>This builder.</returns>
    /// <exception cref="ArgumentNullException">The name or the permissions are absent.</exception>
    /// <exception cref="ArgumentException">A permission is not well-formed.</exception>
    /// <remarks>
    /// Implements AUTHZ-GRANT-004 and LIB-HOST-001 (D-204). A name outside the rule or
    /// given twice, or a permission not declared, is refused as the model is built.
    /// </remarks>
    public AuthorizationDeclarationBuilder Role(string name, params IReadOnlyList<string> permissions) =>
        Role(name, permissions, handsOut: []);

    /// <summary>
    /// Declares a role the deployment starts with, and the roles its holders may hand
    /// out. It is defined at the first start that finds it not yet seeded, where no role
    /// of its name exists, and is the administrators' to change or remove from then on.
    /// </summary>
    /// <param name="name">
    /// The role's name, as <c>POST /admin/roles</c> takes one and never a role chapter
    /// 10 section 3 seeds.
    /// </param>
    /// <param name="permissions">
    /// What holding it permits, each as <c>resource:action</c>: permissions the host
    /// declares, or the library's own other than <c>system:administer</c>.
    /// </param>
    /// <param name="handsOut">
    /// The roles a holder may hand out where the role is held, each one this builder
    /// declares or one chapter 10 section 3 seeds.
    /// </param>
    /// <returns>This builder.</returns>
    /// <exception cref="ArgumentNullException">The name, the permissions or the roles handed out are absent.</exception>
    /// <exception cref="ArgumentException">A permission is not well-formed.</exception>
    /// <remarks>
    /// Implements AUTHZ-GRANT-004, AUTHZ-GRANT-005 and LIB-HOST-001 (D-204, D-212). A
    /// name outside the rule or given twice, a permission not declared, or a role handed
    /// out that is neither declared nor seeded, is refused as the model is built.
    /// </remarks>
    public AuthorizationDeclarationBuilder Role(
        string name,
        IReadOnlyList<string> permissions,
        IReadOnlyList<string> handsOut)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(permissions);
        ArgumentNullException.ThrowIfNull(handsOut);

        var parsed = new List<Permission>(permissions.Count);

        foreach (string permission in permissions)
        {
            parsed.Add(Core.Permission.Parse(permission));
        }

        _roles.Add(new RoleDeclaration(name, parsed, [.. handsOut]));

        return this;
    }

    /// <summary>
    /// Declares one of the host's background jobs. Its principal is
    /// <see cref="SystemPrincipal.ForOrganization"/> naming it, which acts for one
    /// organization.
    /// </summary>
    /// <param name="name">
    /// The job's name, under the rule of INT-SMS-003, distinct from every other job's,
    /// from every principal chapter 10 section 5.29 names and from <c>migrate</c>.
    /// </param>
    /// <param name="permissions">
    /// The host permissions it exercises, each as <c>resource:action</c>: permissions the
    /// host declares, none bound to a step-up gate and none whose action is
    /// <c>export</c>.
    /// </param>
    /// <returns>This builder.</returns>
    /// <exception cref="ArgumentNullException">The name or the permissions are absent.</exception>
    /// <exception cref="ArgumentException">A permission is not well-formed.</exception>
    /// <remarks>
    /// Implements IDN-PRIN-001 and LIB-HOST-001 (D-202, D-206). A name outside the rule,
    /// given twice or reserved, or a permission the job may not exercise, is refused as
    /// the deployment starts.
    /// </remarks>
    public AuthorizationDeclarationBuilder Job(string name, params IReadOnlyList<string> permissions)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(permissions);

        var parsed = new List<Permission>(permissions.Count);

        foreach (string permission in permissions)
        {
            parsed.Add(Core.Permission.Parse(permission));
        }

        _jobs.Add(new JobDeclaration(name, parsed));

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
            new Dictionary<Permission, string>(_actionPurposes),
            [.. _bases],
            [.. _sensitiveCategories],
            [.. _recipients],
            new Dictionary<string, TimeSpan>(_retentionFloors, StringComparer.Ordinal),
            [.. _cookies],
            [.. _roles],
            [.. _jobs]);
}
