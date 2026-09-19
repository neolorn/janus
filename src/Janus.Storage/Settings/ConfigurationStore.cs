using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Core.Configuration;

namespace Janus.Storage.Settings;

/// <summary>
/// The value in force for a configuration key, over the <c>settings</c> table.
/// </summary>
/// <param name="context">The context the operation's writes are tracked on.</param>
/// <remarks>
/// Implements OPS-CFG-008, OPS-CFG-001 and OPS-CFG-004. Every read goes to the table,
/// so a change put in force anywhere in the deployment is seen by the next read
/// without a restart; a key the deployment never wrote has no row and reads as the
/// default the catalogue gives it.
/// </remarks>
internal sealed class ConfigurationStore(JanusDbContext context) : IConfigurationStore
{
    /// <inheritdoc/>
    public async ValueTask<Result<TValue>> ReadAsync<TValue>(
        Setting<TValue> setting,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(setting);

        SettingRecord? record = await RowAsync(setting.Key, cancellationToken).ConfigureAwait(false);

        if (record is not null)
        {
            return setting.Read(record.Value);
        }

        return setting.IsRequired
            ? Result.Failure<TValue>(Undeclared(setting.Key))
            : Result.Success(setting.Default);
    }

    /// <inheritdoc/>
    public async ValueTask<Result<TValue>> ReadAsync<TValue>(
        SettingFamily<TValue> family,
        string parameter,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(family);

        ConfigurationKey key = family.For(parameter);
        SettingRecord? record = await RowAsync(key, cancellationToken).ConfigureAwait(false);

        if (record is not null)
        {
            return family.Read(parameter, record.Value);
        }

        return family.HasDefault
            ? Result.Success(family.Default)
            : Result.Failure<TValue>(Undeclared(key));
    }

    /// <inheritdoc/>
    public async ValueTask<Result<TValue>> WriteAsync<TValue>(
        Setting<TValue> setting,
        TValue value,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(setting);

        if (setting.Scope is SettingScope.Protected)
        {
            return Result.Failure<TValue>(
                new Error(ErrorCodes.ConfigurationKeyProtected, Naming(setting.Key)));
        }

        Result<TValue> accepted = setting.Accept(value);
        Error? refused = null;
        string? written = null;

        accepted.Switch(admitted => written = setting.Write(admitted), failure => refused = failure);

        if (refused is { } failure)
        {
            return Result.Failure<TValue>(failure);
        }

        Result<TValue> before = await ReadAsync(setting, cancellationToken).ConfigureAwait(false);
        SettingRecord? record = await RowAsync(setting.Key, cancellationToken).ConfigureAwait(false);

        if (record is null)
        {
            record = new SettingRecord { Key = setting.Key };
            context.Settings.Add(record);
        }

        record.Value = written!;

        return before;
    }

    private static Error Undeclared(ConfigurationKey key) =>
        new(ErrorCodes.StartupDeclarationMissing, Naming(key));

    private static Dictionary<string, JsonElement> Naming(ConfigurationKey key) =>
        new Dictionary<string, JsonElement>(capacity: 1, StringComparer.Ordinal)
        {
            ["key"] = JsonSerializer.SerializeToElement(key.ToString()),
        };

    private async ValueTask<SettingRecord?> RowAsync(
        ConfigurationKey key,
        CancellationToken cancellationToken) =>
        await context.Settings.FindAsync([key], cancellationToken).ConfigureAwait(false);
}
