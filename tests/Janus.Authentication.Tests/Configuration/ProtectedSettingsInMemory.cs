using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Configuration;
using Janus.Core.Configuration;

namespace Janus.Authentication.Tests.Configuration;

/// <summary>
/// The settings table as the command on the server writes it, holding a value for every
/// key the deployment has to name, as a bootstrapped deployment does.
/// </summary>
internal sealed class ProtectedSettingsInMemory : IProtectedSettings
{
    private readonly Dictionary<ConfigurationKey, string> _written =
        Settings.Required.ToDictionary(setting => setting.Key, _ => string.Empty);

    /// <summary>
    /// What each key holds, in the form the settings table holds it.
    /// </summary>
    public IReadOnlyDictionary<ConfigurationKey, string> Written => _written;

    /// <inheritdoc/>
    public ValueTask<string?> WriteAsync(ConfigurationKey key, string written, CancellationToken cancellationToken)
    {
        string? before = _written.GetValueOrDefault(key);

        _written[key] = written;

        return ValueTask.FromResult(before);
    }

    /// <inheritdoc/>
    public ValueTask<IReadOnlySet<ConfigurationKey>> HeldAsync(CancellationToken cancellationToken) =>
        ValueTask.FromResult<IReadOnlySet<ConfigurationKey>>(_written.Keys.ToHashSet());
}
