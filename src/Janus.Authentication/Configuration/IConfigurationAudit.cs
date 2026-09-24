using System;
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
/// Implements OPS-CFG-005, IDN-PRIN-001 and CONV-DESIGN-003. The trail is the one the permission
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
    /// Records a value bootstrap set, under the principal it runs as. What bootstrap sets
    /// is the value the deployment starts from, named by whoever holds the server, so it
    /// is recorded as no loosening: OPS-CFG-002 prices a change made through the
    /// application.
    /// </summary>
    /// <param name="key">Which setting.</param>
    /// <param name="before">What it read as, or nothing where no value stood.</param>
    /// <param name="after">What it reads as now.</param>
    /// <param name="principal">The principal that set it, with its stated reason.</param>
    /// <param name="at">When.</param>
    /// <param name="cancellationToken">Abandons the write.</param>
    /// <returns>The work of recording it.</returns>
    ValueTask ChangedAsync(
        ConfigurationKey key,
        string? before,
        string after,
        SystemPrincipal principal,
        DateTimeOffset at,
        CancellationToken cancellationToken);

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
