using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Configuration;
using Janus.Core;
using Janus.Core.Configuration;

namespace Janus.Hosting.Configuration;

/// <summary>
/// Reading one key as the administration interface answers it, whatever the type of
/// its value.
/// </summary>
/// <param name="configuration">Where the value in force is read.</param>
/// <param name="cancellationToken">Abandons the read.</param>
/// <remarks>
/// Implements OPS-CFG-004, OPS-CFG-008 and chapter 09 section 8 (D-153, D-277). A runtime
/// key the deployment names that was never written reads with no value, which its first
/// change then gives it.
/// </remarks>
internal sealed class SettingReading(
    IConfigurationStore configuration,
    CancellationToken cancellationToken) : ISettingOperation<ValueTask<Result<ConfiguredSetting>>>
{
    private static readonly JsonElement Nothing = JsonSerializer.SerializeToElement<object?>(null);

    /// <inheritdoc/>
    public ValueTask<Result<ConfiguredSetting>> On<TValue>(Setting<TValue> setting) =>
        ReadAsync(setting, cancellationToken);

    private async ValueTask<Result<ConfiguredSetting>> ReadAsync<TValue>(
        Setting<TValue> setting,
        CancellationToken cancellationToken)
    {
        Result<TValue> read = await configuration.ReadAsync(setting, cancellationToken).ConfigureAwait(false);

        if (ConfigurationAdministration.Unwritten(setting, read))
        {
            return Result.Success(Configured(setting, Nothing));
        }

        return read.Match(value => Result.Success(Configured(setting, setting.Json(value))), Result.Failure<ConfiguredSetting>);
    }

    private static ConfiguredSetting Configured<TValue>(Setting<TValue> setting, JsonElement value) =>
        new(
            setting.Key,
            value,
            setting.IsRequired ? null : setting.Json(setting.Default),
            setting.Scope is SettingScope.Protected,
            setting.Loosening);
}
