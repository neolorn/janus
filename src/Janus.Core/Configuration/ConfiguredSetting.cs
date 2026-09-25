using System.Text.Json;

namespace Janus.Core.Configuration;

/// <summary>
/// One key as the administration interface reads it: the value in force, the default,
/// whether the application may change it, and which way a change loosens.
/// </summary>
/// <param name="Key">The key.</param>
/// <param name="Value">The value in force, in the key's own type.</param>
/// <param name="Default">The shipped default, or nothing for a key the deployment names.</param>
/// <param name="Protected">Whether the key is not changeable through the application.</param>
/// <param name="Direction">Which way a change to it loosens the deployment.</param>
/// <remarks>Implements OPS-CFG-002, OPS-CFG-004 and chapter 09 section 8 (D-153).</remarks>
public sealed record ConfiguredSetting(
    ConfigurationKey Key,
    JsonElement Value,
    JsonElement? Default,
    bool Protected,
    SettingDirection Direction);
