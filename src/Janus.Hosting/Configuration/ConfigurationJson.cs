using System.Text.Json.Serialization;

namespace Janus.Hosting.Configuration;

/// <summary>
/// How the configuration administration reads and writes, generated rather than
/// reflected over (CONV-CODE-004, CONV-DESIGN-006).
/// </summary>
/// <remarks>Implements chapter 09 section 8 and CONV-DESIGN-006.</remarks>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UseStringEnumConverter = true)]
[JsonSerializable(typeof(ConfigurationBody))]
[JsonSerializable(typeof(ConfiguredSettingView))]
internal sealed partial class ConfigurationJson : JsonSerializerContext;
