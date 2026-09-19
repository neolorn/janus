using System;
using Janus.Core;

namespace Janus.Authorization.Groups;

/// <summary>
/// A set of subjects that holds grants on their behalf. Groups nest, and membership
/// is followed at any depth.
/// </summary>
/// <remarks>
/// Implements AUTHZ-GROUP-001 and AUTHZ-GROUP-002. The members are rows of their own,
/// because the set a principal belongs to is resolved once per request from a
/// precomputed closure rather than walked at each check.
/// </remarks>
internal sealed class Group
{
    private Group(GroupId id, OrganizationId organization, string name)
    {
        Id = id;
        Organization = organization;
        Name = name;
    }

    /// <summary>
    /// The identifier a grant and a membership name.
    /// </summary>
    public GroupId Id { get; }

    /// <summary>
    /// The organization the group belongs to.
    /// </summary>
    public OrganizationId Organization { get; }

    /// <summary>
    /// What the group is called.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// A new group.
    /// </summary>
    /// <param name="id">The identifier issued for it.</param>
    /// <param name="organization">The organization it belongs to.</param>
    /// <param name="name">What it is called.</param>
    /// <returns>The group.</returns>
    /// <exception cref="ArgumentException">The name is absent or blank.</exception>
    public static Group Create(GroupId id, OrganizationId organization, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        return new Group(id, organization, name.Trim());
    }

    /// <summary>
    /// A group as its row holds it.
    /// </summary>
    /// <param name="id">The identifier.</param>
    /// <param name="organization">The organization it belongs to.</param>
    /// <param name="name">What it is called.</param>
    /// <returns>The group.</returns>
    public static Group Existing(GroupId id, OrganizationId organization, string name) =>
        new(id, organization, name);
}
