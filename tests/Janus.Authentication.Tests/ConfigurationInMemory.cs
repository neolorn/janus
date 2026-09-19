using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Core.Configuration;

namespace Janus.Authentication.Tests;

/// <summary>
/// The configuration store, holding what a test wrote and answering everything else
/// with the key's default.
/// </summary>
internal sealed class ConfigurationInMemory : IConfigurationStore
{
    private readonly Dictionary<ConfigurationKey, object> _values = [];

    /// <summary>
    /// Names a value for a key, as a deployment does.
    /// </summary>
    /// <typeparam name="TValue">The type of the setting's value.</typeparam>
    /// <param name="setting">The setting.</param>
    /// <param name="value">The value.</param>
    public void Set<TValue>(Setting<TValue> setting, TValue value)
        where TValue : notnull =>
        _values[setting.Key] = value;

    /// <inheritdoc/>
    public ValueTask<Result<TValue>> ReadAsync<TValue>(
        Setting<TValue> setting,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult(Result.Success(
            _values.TryGetValue(setting.Key, out object? written)
                ? (TValue)written
                : setting.Default));

    /// <inheritdoc/>
    public ValueTask<Result<TValue>> ReadAsync<TValue>(
        SettingFamily<TValue> family,
        string parameter,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult(Result.Success(
            _values.TryGetValue(ConfigurationKey.Parse(family.Prefix + "." + parameter), out object? written)
                ? (TValue)written
                : family.Default));

    /// <summary>
    /// Names a value for one member of a family.
    /// </summary>
    /// <typeparam name="TValue">The type of the member's value.</typeparam>
    /// <param name="family">The family.</param>
    /// <param name="parameter">The member.</param>
    /// <param name="value">The value.</param>
    public void Set<TValue>(SettingFamily<TValue> family, string parameter, TValue value)
        where TValue : notnull =>
        _values[ConfigurationKey.Parse(family.Prefix + "." + parameter)] = value;
}
