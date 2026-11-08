using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authorization.Roles;
using Janus.Core;

namespace Janus.Authorization.Tests.Roles;

/// <summary>
/// The inclusions of roles in one another a deployment wrote, and the actions it declared
/// not inherited, held in memory.
/// </summary>
/// <remarks>
/// CONV-TEST-004: a fake that answers from what it holds, so a test that makes an
/// inclusion sees it in what every role then carries (AUTHZ-GRANT-006).
/// </remarks>
internal sealed class RoleInclusionsInMemory : IRoleInclusionStore
{
    private readonly List<(RoleName Role, RoleInclusion Inclusion)> _inclusions = [];

    /// <summary>
    /// The actions the last start declared not inherited.
    /// </summary>
    public IReadOnlySet<Permission> Uninherited { get; private set; } = new HashSet<Permission>();

    /// <summary>
    /// Every inclusion held, by the including role.
    /// </summary>
    public IReadOnlyList<(RoleName Role, RoleInclusion Inclusion)> Held => _inclusions;

    /// <summary>
    /// How many times the inclusions were held, which a change to one takes before it is
    /// judged again.
    /// </summary>
    public int Holds { get; private set; }

    /// <inheritdoc/>
    public ValueTask HoldAsync(CancellationToken cancellationToken)
    {
        Holds++;

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask<IReadOnlyList<(RoleName Role, RoleInclusion Inclusion)>> AllAsync(
        CancellationToken cancellationToken) =>
        ValueTask.FromResult<IReadOnlyList<(RoleName Role, RoleInclusion Inclusion)>>([.. _inclusions]);

    /// <inheritdoc/>
    public ValueTask CreateAsync(RoleName role, RoleInclusion inclusion, CancellationToken cancellationToken)
    {
        _inclusions.Add((role, inclusion));

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask RecordAsync(RoleName role, RoleInclusion inclusion, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(inclusion);

        int at = _inclusions.FindIndex(held => held.Role == role && held.Inclusion.Included == inclusion.Included);

        if (at < 0)
        {
            throw new InvalidOperationException("The inclusion has no row to carry it onto.");
        }

        _inclusions[at] = (role, inclusion);

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask RemoveAsync(RoleName role, RoleName included, CancellationToken cancellationToken)
    {
        _ = _inclusions.RemoveAll(held => held.Role == role && held.Inclusion.Included == included);

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask DeclareUninheritedAsync(IReadOnlySet<Permission> permissions, CancellationToken cancellationToken)
    {
        Uninherited = permissions.ToHashSet();

        return ValueTask.CompletedTask;
    }
}
