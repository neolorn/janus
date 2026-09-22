using System.Collections.Generic;
using System.Text.Json;
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
    private static readonly Dictionary<string, JsonElement> Nothing = [];

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
        ValueTask.FromResult(Read(setting));

    /// <inheritdoc/>
    public ValueTask<Result<TValue>> ReadAsync<TValue>(
        SettingFamily<TValue> family,
        string parameter,
        CancellationToken cancellationToken)
    {
        if (_values.TryGetValue(
            ConfigurationKey.Parse(family.Prefix + "." + parameter),
            out object? written))
        {
            return ValueTask.FromResult(Result.Success((TValue)written));
        }

        // A family with no default is one the deployment names per parameter, and an
        // unnamed one is undeclared rather than a value nobody wrote down.
        return ValueTask.FromResult(family.HasDefault
            ? Result.Success(family.Default)
            : Result.Failure<TValue>(new Error(ErrorCodes.StartupDeclarationMissing, Nothing)));
    }

    /// <inheritdoc/>
    public ValueTask<Result<TValue>> WriteAsync<TValue>(
        Setting<TValue> setting,
        TValue value,
        CancellationToken cancellationToken)
    {
        if (setting.Scope is SettingScope.Protected)
        {
            return ValueTask.FromResult(
                Result.Failure<TValue>(new Error(ErrorCodes.ConfigurationKeyProtected, Nothing)));
        }

        Result<TValue> before = Read(setting);

        return ValueTask.FromResult(setting.Accept(value).Match(
            admitted =>
            {
                _values[setting.Key] = admitted!;
                return before;
            },
            Result.Failure<TValue>));
    }

    // A required key the deployment never named is undeclared, not a value nobody
    // wrote down; the store answers it the same way (LIB-HOST-001).
    private Result<TValue> Read<TValue>(Setting<TValue> setting)
    {
        if (_values.TryGetValue(setting.Key, out object? written))
        {
            return Result.Success((TValue)written);
        }

        return setting.IsRequired
            ? Result.Failure<TValue>(new Error(ErrorCodes.StartupDeclarationMissing, Nothing))
            : Result.Success(setting.Default);
    }

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
