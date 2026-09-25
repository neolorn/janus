using System.Threading;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Core.Configuration;

namespace Janus.Hosting.Configuration;

/// <summary>
/// Reading one key as the administration interface answers it, whatever the type of
/// its value.
/// </summary>
/// <param name="configuration">Where the value in force is read.</param>
/// <param name="cancellationToken">Abandons the read.</param>
/// <remarks>Implements OPS-CFG-004 and chapter 09 section 8 (D-153).</remarks>
internal sealed class SettingReading(
    IConfigurationStore configuration,
    CancellationToken cancellationToken) : ISettingOperation<ValueTask<Result<ConfiguredSetting>>>
{
    /// <inheritdoc/>
    public async ValueTask<Result<ConfiguredSetting>> On<TValue>(Setting<TValue> setting) =>
        (await configuration.ReadAsync(setting, cancellationToken).ConfigureAwait(false)).Match(
            value => Result.Success(
                new ConfiguredSetting(
                    setting.Key,
                    setting.Json(value),
                    setting.IsRequired ? null : setting.Json(setting.Default),
                    setting.Scope is SettingScope.Protected,
                    setting.Loosening)),
            Result.Failure<ConfiguredSetting>);
}
