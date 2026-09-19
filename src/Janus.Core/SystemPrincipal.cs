using System;
using System.Collections.Generic;

namespace Janus.Core;

/// <summary>
/// What a background job, an import or a webhook acts as: a named principal with a
/// stated reason and a scope it cannot reach past. There is no such thing as no user,
/// therefore allow.
/// </summary>
/// <remarks>
/// Implements IDN-PRIN-001. Two scopes exist. An organization-scoped principal acts for
/// one organization and cannot read across them. A deployment-scoped principal exists
/// for work that is inherently pool-wide, is restricted to the operations it names, and
/// serves no request.
/// </remarks>
public sealed class SystemPrincipal
{
    private readonly IReadOnlySet<SystemOperation> _operations;

    private SystemPrincipal(
        string name,
        string reason,
        OrganizationId? organization,
        IReadOnlySet<SystemOperation> operations)
    {
        Name = name;
        Reason = reason;
        Organization = organization;
        _operations = operations;
    }

    /// <summary>
    /// What the principal is called, which is what an audit record names as the actor.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Why it is acting, which every action it takes is audited with.
    /// </summary>
    public string Reason { get; }

    /// <summary>
    /// The one organization it acts for, or nothing where it is deployment-scoped.
    /// </summary>
    public OrganizationId? Organization { get; }

    /// <summary>
    /// The pool-wide operations it may run. Empty for an organization-scoped principal,
    /// which runs none of them.
    /// </summary>
    public IReadOnlySet<SystemOperation> Operations => _operations;

    /// <summary>
    /// Whether the principal is one that reaches across organizations.
    /// </summary>
    public bool IsDeploymentScoped => Organization is null;

    /// <summary>
    /// A principal acting for one organization.
    /// </summary>
    /// <param name="name">What it is called.</param>
    /// <param name="reason">Why it is acting.</param>
    /// <param name="organization">The organization it acts for.</param>
    /// <returns>The principal.</returns>
    /// <exception cref="ArgumentException">The name or the reason is absent or blank.</exception>
    public static SystemPrincipal ForOrganization(string name, string reason, OrganizationId organization)
    {
        Stated(name, reason);

        return new SystemPrincipal(name, reason, organization, new HashSet<SystemOperation>());
    }

    /// <summary>
    /// A principal for work that is inherently pool-wide, restricted to the operations
    /// it names.
    /// </summary>
    /// <param name="name">What it is called.</param>
    /// <param name="reason">Why it is acting.</param>
    /// <param name="operations">The operations it may run.</param>
    /// <returns>The principal.</returns>
    /// <exception cref="ArgumentNullException">The operations are absent.</exception>
    /// <exception cref="ArgumentException">
    /// The name or the reason is absent or blank, or no operation is named.
    /// </exception>
    public static SystemPrincipal ForDeployment(
        string name,
        string reason,
        params IReadOnlyList<SystemOperation> operations)
    {
        Stated(name, reason);
        ArgumentNullException.ThrowIfNull(operations);

        if (operations.Count == 0)
        {
            throw new ArgumentException(
                "A deployment-scoped principal names the operations it exists for.",
                nameof(operations));
        }

        return new SystemPrincipal(name, reason, organization: null, new HashSet<SystemOperation>(operations));
    }

    /// <summary>
    /// Whether the principal may act for an organization. An organization-scoped one
    /// may act for its own and no other; a deployment-scoped one acts for none, because
    /// what it does is pool-wide and not on behalf of anybody.
    /// </summary>
    /// <param name="organization">The organization in question.</param>
    /// <returns>Whether it may.</returns>
    public bool ActsFor(OrganizationId organization) => Organization == organization;

    /// <summary>
    /// Whether the principal may run a pool-wide operation.
    /// </summary>
    /// <param name="operation">The operation.</param>
    /// <returns>Whether it may.</returns>
    public bool MayRun(SystemOperation operation) => _operations.Contains(operation);

    private static void Stated(string name, string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("A system principal states why it is acting.", nameof(reason));
        }
    }
}
