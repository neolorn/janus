using System.Text.Json.Serialization;

namespace Janus.Hosting.BreakGlass;

/// <summary>
/// How the break-glass endpoints read and write, generated rather than reflected over
/// (CONV-CODE-004, CONV-DESIGN-006).
/// </summary>
/// <remarks>Implements API-CONV-002 and CONV-DESIGN-006.</remarks>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(PresentBreakGlassRequest))]
[JsonSerializable(typeof(GeneratedBreakGlassView))]
internal sealed partial class BreakGlassJson : JsonSerializerContext;
