using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authorization.Model;
using Janus.Core;
using Janus.Core.Configuration;

namespace Janus.Authorization.Roles;

/// <summary>
/// Reads the deployment's roles and their inclusions as one graph.
/// </summary>
/// <param name="roles">Where roles are read.</param>
/// <param name="inclusions">Where inclusions are read.</param>
/// <param name="model">Which of the host's actions are not inherited.</param>
/// <param name="configuration">Whether inclusions admit at all.</param>
/// <remarks>Implements AUTHZ-GRANT-006 (D-278).</remarks>
internal sealed class RoleGraphs(
    IRoleStore roles,
    IRoleInclusionStore inclusions,
    AuthorizationModel model,
    IConfigurationStore configuration)
{
    /// <summary>
    /// The graph as a grant admits through it now, with <c>authz.roleinclusion.enabled</c>
    /// as it reads.
    /// </summary>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The graph.</returns>
    /// <remarks>OPS-CFG-002: a value that cannot be read leaves the stricter rule, off.</remarks>
    public async ValueTask<RoleGraph> ReadAsync(CancellationToken cancellationToken)
    {
        bool enabled = (await configuration
                .ReadAsync(Settings.AuthzRoleInclusionEnabled, cancellationToken)
                .ConfigureAwait(false))
            .Match(read => read, _ => false);

        return await ReadAsync(enabled, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// The graph with every inclusion that is on followed, whatever
    /// <c>authz.roleinclusion.enabled</c> reads: what an inclusion would pass on, which a
    /// change to one is judged on, so a change made while the key is off reaches no
    /// further than one made while it is on.
    /// </summary>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The graph.</returns>
    public async ValueTask<RoleGraph> ReadFollowingAsync(CancellationToken cancellationToken) =>
        await ReadAsync(enabled: true, cancellationToken).ConfigureAwait(false);

    private async ValueTask<RoleGraph> ReadAsync(bool enabled, CancellationToken cancellationToken)
    {
        IReadOnlyList<Role> held = await roles.AllAsync(cancellationToken).ConfigureAwait(false);
        IReadOnlyList<(RoleName Role, RoleInclusion Inclusion)> included = await inclusions
            .AllAsync(cancellationToken)
            .ConfigureAwait(false);

        return RoleGraph.Of(held, included, model.NotInherited, enabled);
    }
}
