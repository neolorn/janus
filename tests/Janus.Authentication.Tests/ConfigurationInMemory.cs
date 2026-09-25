using System;
using System.Collections.Generic;
using System.Linq;
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
    /// The key whose every read throws, as a store that has gone away does; nothing
    /// while every key reads.
    /// </summary>
    public ConfigurationKey? Unreachable { get; set; }

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
        setting.Key.Equals(Unreachable)
            ? throw new InvalidOperationException("The settings table at db.internal:5432 could not be reached.")
            : ValueTask.FromResult(Read(setting));

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
    public ValueTask<Result<IReadOnlyDictionary<string, TValue>>> ReadWrittenAsync<TValue>(
        SettingFamily<TValue> family,
        CancellationToken cancellationToken)
    {
        string prefix = family.Prefix + ".";

        Dictionary<string, TValue> written = new(StringComparer.Ordinal);

        foreach (KeyValuePair<ConfigurationKey, object> held in _values)
        {
            string key = held.Key.ToString();

            // A key that exists once for the deployment can sit under a family's
            // prefix, as policy.default sits under policy; the catalogue is what says
            // it is not a member of the family.
            if (key.StartsWith(prefix, StringComparison.Ordinal)
                && !Settings.All.Any(setting => setting.Key == held.Key))
            {
                written[key[prefix.Length..]] = (TValue)held.Value;
            }
        }

        return ValueTask.FromResult(
            Result.Success<IReadOnlyDictionary<string, TValue>>(written));
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

    /// <inheritdoc/>
    public async ValueTask<Result<TValue>> WriteAsync<TValue>(
        SettingFamily<TValue> family,
        string parameter,
        TValue value,
        CancellationToken cancellationToken)
    {
        if (family.Scope is SettingScope.Protected)
        {
            return Result.Failure<TValue>(new Error(ErrorCodes.ConfigurationKeyProtected, Nothing));
        }

        Result<TValue> before = await ReadAsync(family, parameter, cancellationToken);

        return family.Read(parameter, family.Write(value)).Match(
            admitted =>
            {
                _values[family.For(parameter)] = admitted!;
                return before;
            },
            Result.Failure<TValue>);
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
