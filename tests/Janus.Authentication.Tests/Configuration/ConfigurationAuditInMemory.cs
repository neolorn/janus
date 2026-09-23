using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Configuration;
using Janus.Core;
using Janus.Core.Configuration;

namespace Janus.Authentication.Tests.Configuration;

/// <summary>
/// The configuration trail, holding what was written to it so a test can read what a
/// change recorded and read it back the two ways the chapter asks for.
/// </summary>
internal sealed class ConfigurationAuditInMemory : IConfigurationAudit
{
    /// <summary>
    /// Every change recorded, in the order it was made.
    /// </summary>
    public List<ConfigurationChange> Written { get; } = [];

    /// <inheritdoc/>
    public ValueTask ChangedAsync(ConfigurationChange change, CancellationToken cancellationToken)
    {
        Written.Add(change);

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask<IReadOnlyList<ConfigurationChange>> OfSettingAsync(
        ConfigurationKey key,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult<IReadOnlyList<ConfigurationChange>>(
            [.. Written.Where(change => change.Key == key).Reverse()]);

    /// <inheritdoc/>
    public ValueTask<IReadOnlyList<ConfigurationChange>> OfActorAsync(
        SubjectId actor,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult<IReadOnlyList<ConfigurationChange>>(
            [.. Written.Where(change => change.Actor == actor).Reverse()]);
}
