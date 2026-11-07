using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Janus.Core;

namespace Janus.Authorization.Roles;

/// <summary>
/// Every role the deployment holds and every inclusion of one in another, read at one
/// instant, and what each role carries and hands out once its inclusions are followed.
/// </summary>
/// <remarks>
/// Implements AUTHZ-GRANT-006 (D-278). A role's effective permissions are its own and,
/// through each inclusion that is on, the included role's effective permissions, save the
/// actions not inherited, <c>system:administer</c> and what the inclusion leaves out; its
/// effective <c>handsOut</c> likewise. With <c>authz.roleinclusion.enabled</c> off, each
/// role carries and hands out its own alone. Nothing is copied into a role: the graph is
/// read where access is worked out, and the view a grant is read through holds the same
/// rule for the renderings. Each entry is listed once, its source the role's own where it
/// carries it itself, otherwise the shortest path of inclusions, and among paths of one
/// length the first by the ordinal order of the role names along it.
/// </remarks>
internal sealed class RoleGraph
{
    private readonly ImmutableDictionary<RoleName, Role> _roles;
    private readonly ImmutableDictionary<RoleName, ImmutableList<RoleInclusion>> _inclusions;
    private readonly IReadOnlySet<Permission> _uninherited;
    private readonly bool _enabled;
    private readonly Dictionary<RoleName, Reached<Permission>> _permissions = [];
    private readonly Dictionary<RoleName, Reached<RoleName>> _handsOut = [];

    private RoleGraph(
        ImmutableDictionary<RoleName, Role> roles,
        ImmutableDictionary<RoleName, ImmutableList<RoleInclusion>> inclusions,
        IReadOnlySet<Permission> uninherited,
        bool enabled)
    {
        _roles = roles;
        _inclusions = inclusions;
        _uninherited = uninherited;
        _enabled = enabled;
    }

    /// <summary>
    /// The graph of the roles and inclusions given.
    /// </summary>
    /// <param name="roles">Every role, with its own permissions and <c>handsOut</c>.</param>
    /// <param name="inclusions">Every inclusion, by the including role.</param>
    /// <param name="uninherited">The host's actions declared not inherited.</param>
    /// <param name="enabled">Whether inclusions admit at all.</param>
    /// <returns>The graph.</returns>
    /// <exception cref="ArgumentNullException">A part is absent.</exception>
    public static RoleGraph Of(
        IEnumerable<Role> roles,
        IEnumerable<(RoleName Role, RoleInclusion Inclusion)> inclusions,
        IReadOnlySet<Permission> uninherited,
        bool enabled)
    {
        ArgumentNullException.ThrowIfNull(roles);
        ArgumentNullException.ThrowIfNull(inclusions);
        ArgumentNullException.ThrowIfNull(uninherited);

        return new RoleGraph(
            roles.ToImmutableDictionary(role => role.Name),
            inclusions
                .GroupBy(pair => pair.Role)
                .ToImmutableDictionary(
                    group => group.Key,
                    group => group.Select(pair => pair.Inclusion).ToImmutableList()),
            uninherited,
            enabled);
    }

    /// <summary>
    /// Every role the graph holds, by name.
    /// </summary>
    public IEnumerable<Role> Roles => _roles.Values.OrderBy(role => role.Name.ToString(), StringComparer.Ordinal);

    /// <summary>
    /// One role, or nothing where the graph holds none of that name.
    /// </summary>
    /// <param name="name">Which role.</param>
    /// <returns>The role, or nothing.</returns>
    public Role? Find(RoleName name) => _roles.GetValueOrDefault(name);

    /// <summary>
    /// A role's inclusions of other roles, by the included role's name.
    /// </summary>
    /// <param name="role">The including role.</param>
    /// <returns>Its inclusions, none where it includes nothing.</returns>
    public IReadOnlyList<RoleInclusion> InclusionsOf(RoleName role) =>
        [.. (_inclusions.GetValueOrDefault(role) ?? []).OrderBy(inclusion => inclusion.Included.ToString(), StringComparer.Ordinal)];

    /// <summary>
    /// A role's inclusion of another, or nothing where it does not include it.
    /// </summary>
    /// <param name="role">The including role.</param>
    /// <param name="included">The role included.</param>
    /// <returns>The inclusion, or nothing.</returns>
    public RoleInclusion? Inclusion(RoleName role, RoleName included) =>
        _inclusions.GetValueOrDefault(role)?.Find(inclusion => inclusion.Included == included);

    /// <summary>
    /// The roles that include the one given, on or off.
    /// </summary>
    /// <param name="included">The role included.</param>
    /// <returns>The including roles.</returns>
    public IEnumerable<RoleName> Including(RoleName included) =>
        _inclusions
            .Where(pair => pair.Value.Exists(inclusion => inclusion.Included == included))
            .Select(pair => pair.Key);

    /// <summary>
    /// Whether one role reaches another through inclusions, on or off, at any depth.
    /// </summary>
    /// <param name="from">The role followed from.</param>
    /// <param name="sought">The role sought.</param>
    /// <returns>Whether it reaches it.</returns>
    public bool Reaches(RoleName from, RoleName sought)
    {
        var followed = new HashSet<RoleName>();
        var waiting = new Stack<RoleName>([from]);

        while (waiting.TryPop(out RoleName at))
        {
            if (at == sought)
            {
                return true;
            }

            if (!followed.Add(at))
            {
                continue;
            }

            foreach (RoleInclusion inclusion in _inclusions.GetValueOrDefault(at) ?? [])
            {
                waiting.Push(inclusion.Included);
            }
        }

        return false;
    }

    /// <summary>
    /// The graph with a role as given, its inclusions as they stand.
    /// </summary>
    /// <param name="role">The role as it would stand.</param>
    /// <returns>The graph it would make.</returns>
    public RoleGraph With(Role role)
    {
        ArgumentNullException.ThrowIfNull(role);

        return new RoleGraph(_roles.SetItem(role.Name, role), _inclusions, _uninherited, _enabled);
    }

    /// <summary>
    /// The graph with a role's inclusion of another as given.
    /// </summary>
    /// <param name="role">The including role.</param>
    /// <param name="inclusion">The inclusion as it would stand.</param>
    /// <returns>The graph it would make.</returns>
    public RoleGraph With(RoleName role, RoleInclusion inclusion)
    {
        ArgumentNullException.ThrowIfNull(inclusion);

        ImmutableList<RoleInclusion> held = (_inclusions.GetValueOrDefault(role) ?? [])
            .RemoveAll(each => each.Included == inclusion.Included)
            .Add(inclusion);

        return new RoleGraph(_roles, _inclusions.SetItem(role, held), _uninherited, _enabled);
    }

    /// <summary>
    /// The graph without a role's inclusion of another.
    /// </summary>
    /// <param name="role">The including role.</param>
    /// <param name="included">The role it would no longer include.</param>
    /// <returns>The graph it would make.</returns>
    public RoleGraph Without(RoleName role, RoleName included)
    {
        ImmutableList<RoleInclusion> held = (_inclusions.GetValueOrDefault(role) ?? [])
            .RemoveAll(each => each.Included == included);

        return new RoleGraph(_roles, _inclusions.SetItem(role, held), _uninherited, _enabled);
    }

    /// <summary>
    /// What a role carries and hands out once its inclusions are followed, or nothing
    /// where the graph holds no role of that name.
    /// </summary>
    /// <param name="role">Which role.</param>
    /// <returns>Each permission and each role handed out once, by name, with its source.</returns>
    public EffectiveRole Effective(RoleName role) =>
        new(
            [.. Permissions(role).Select(pair => new EffectiveEntry<Permission>(pair.Key, pair.Value))
                .OrderBy(entry => entry.Name.ToString(), StringComparer.Ordinal)],
            [.. HandsOut(role).Select(pair => new EffectiveEntry<RoleName>(pair.Key, pair.Value))
                .OrderBy(entry => entry.Name.ToString(), StringComparer.Ordinal)]);

    /// <summary>
    /// The permissions a grant of the role admits, its own and those its inclusions pass
    /// on, each with the roles it came through, nearest first.
    /// </summary>
    /// <param name="role">Which role.</param>
    /// <returns>The permissions, none where the graph holds no such role.</returns>
    public IReadOnlyDictionary<Permission, IReadOnlyList<RoleName>> Permissions(RoleName role) =>
        Reach(role, _permissions, held => held.Permissions, Passes, []);

    /// <summary>
    /// The roles a holder of the role may hand out by it, its own and those its
    /// inclusions pass on, each with the roles it came through, nearest first.
    /// </summary>
    /// <param name="role">Which role.</param>
    /// <returns>The roles, none where the graph holds no such role.</returns>
    public IReadOnlyDictionary<RoleName, IReadOnlyList<RoleName>> HandsOut(RoleName role) =>
        Reach(role, _handsOut, held => held.HandsOut, (inclusion, handed) => !inclusion.LeftOutHandsOut.Contains(handed), []);

    /// <summary>
    /// The role as the hand-out rule judges it: its name, its effective permissions and
    /// <c>handsOut</c>, and whether its grants must end.
    /// </summary>
    /// <param name="role">The role.</param>
    /// <returns>The role as judged.</returns>
    public DefinedRole Judged(Role role)
    {
        ArgumentNullException.ThrowIfNull(role);

        return new DefinedRole(
            role.Name,
            [.. Permissions(role.Name).Keys.OrderBy(permission => permission.ToString(), StringComparer.Ordinal)],
            [.. HandsOut(role.Name).Keys.OrderBy(handed => handed.ToString(), StringComparer.Ordinal)],
            role.GrantsMustEnd);
    }

    // The order a source is preferred in: the role's own, then the shorter path, then the
    // first by the ordinal order of the names along it.
    private static int Compare(IReadOnlyList<RoleName> one, IReadOnlyList<RoleName> other)
    {
        int length = one.Count.CompareTo(other.Count);

        if (length != 0)
        {
            return length;
        }

        for (int index = 0; index < one.Count; index++)
        {
            int named = string.CompareOrdinal(one[index].ToString(), other[index].ToString());

            if (named != 0)
            {
                return named;
            }
        }

        return 0;
    }

    // AUTHZ-GRANT-006, OPS-CFG-007: a duty and system administration pass through no
    // inclusion, and neither does what the inclusion leaves out.
    private bool Passes(RoleInclusion inclusion, Permission permission) =>
        permission != Core.Permissions.SystemAdminister
        && !_uninherited.Contains(permission)
        && !inclusion.LeftOutPermissions.Contains(permission);

    private Dictionary<T, IReadOnlyList<RoleName>> Reach<T>(
        RoleName role,
        Dictionary<RoleName, Reached<T>> known,
        Func<Role, IEnumerable<T>> own,
        Func<RoleInclusion, T, bool> passes,
        HashSet<RoleName> following)
        where T : notnull
    {
        if (known.TryGetValue(role, out Reached<T>? reached))
        {
            return reached.Sources;
        }

        var sources = new Dictionary<T, IReadOnlyList<RoleName>>();

        if (_roles.GetValueOrDefault(role) is not Role held || !following.Add(role))
        {
            return sources;
        }

        foreach (T item in own(held))
        {
            sources[item] = [];
        }

        if (_enabled)
        {
            foreach (RoleInclusion inclusion in (_inclusions.GetValueOrDefault(role) ?? []).Where(each => each.Enabled))
            {
                foreach ((T item, IReadOnlyList<RoleName> through) in
                    Reach(inclusion.Included, known, own, passes, following))
                {
                    if (!passes(inclusion, item))
                    {
                        continue;
                    }

                    IReadOnlyList<RoleName> path = [inclusion.Included, .. through];

                    if (!sources.TryGetValue(item, out IReadOnlyList<RoleName>? standing) || Compare(path, standing) < 0)
                    {
                        sources[item] = path;
                    }
                }
            }
        }

        following.Remove(role);
        known[role] = new Reached<T>(sources);

        return sources;
    }

    // What one role reaches, kept once worked out for the instant the graph was read.
    private sealed record Reached<T>(Dictionary<T, IReadOnlyList<RoleName>> Sources)
        where T : notnull;
}
