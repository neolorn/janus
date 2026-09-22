using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Core.Configuration;

namespace Janus.Authentication.Configuration;

/// <summary>
/// What is written down when a runtime setting changes: who, what, from, to, when and
/// why, and read back by the setting or by the actor.
/// </summary>
/// <remarks>
/// Implements OPS-CFG-005 and CONV-DESIGN-003. The trail is the one the permission
/// grants are written to, under the same retention, so a configuration change is read
/// beside the grant it was made to enable.
/// </remarks>
internal interface IConfigurationAudit
{
    /// <summary>
    /// Records one change.
    /// </summary>
    /// <param name="change">What changed, from what to what, by whom and why.</param>
    /// <param name="cancellationToken">Abandons the write.</param>
    /// <returns>The work of recording it.</returns>
    ValueTask ChangedAsync(ConfigurationChange change, CancellationToken cancellationToken);

    /// <summary>
    /// Every change made to one setting, most recent first.
    /// </summary>
    /// <param name="key">The setting.</param>
    /// <param name="cancellationToken">Abandons the read.</param>
    /// <returns>The changes.</returns>
    ValueTask<IReadOnlyList<ConfigurationChange>> OfSettingAsync(
        ConfigurationKey key,
        CancellationToken cancellationToken);

    /// <summary>
    /// Every change made by one actor, most recent first.
    /// </summary>
    /// <param name="actor">Who made them.</param>
    /// <param name="cancellationToken">Abandons the read.</param>
    /// <returns>The changes.</returns>
    ValueTask<IReadOnlyList<ConfigurationChange>> OfActorAsync(
        SubjectId actor,
        CancellationToken cancellationToken);
}
