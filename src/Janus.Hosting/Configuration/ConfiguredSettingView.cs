using System;
using System.Text.Json;
using Janus.Core.Configuration;

namespace Janus.Hosting.Configuration;

/// <summary>
/// One key as the administration interface answers it.
/// </summary>
/// <param name="Key">The key.</param>
/// <param name="Value">The value in force, in the key's own type.</param>
/// <param name="Default">The shipped default, or nothing for a key the deployment names.</param>
/// <param name="Protected">Whether the key is not changeable through the application.</param>
/// <param name="Direction">Which way a change to it loosens the deployment.</param>
/// <remarks>Implements chapter 09 section 8 (D-153) and OPS-CFG-004.</remarks>
internal sealed record ConfiguredSettingView(
    string Key,
    JsonElement Value,
    JsonElement? Default,
    bool Protected,
    SettingDirection Direction)
{
    /// <summary>
    /// The view of one key.
    /// </summary>
    /// <param name="setting">The key as the contract reads it.</param>
    /// <returns>The view.</returns>
    /// <exception cref="ArgumentNullException">The key is absent.</exception>
    public static ConfiguredSettingView Of(ConfiguredSetting setting)
    {
        ArgumentNullException.ThrowIfNull(setting);

        return new ConfiguredSettingView(
            setting.Key.ToString(),
            setting.Value,
            setting.Default,
            setting.Protected,
            setting.Direction);
    }
}
