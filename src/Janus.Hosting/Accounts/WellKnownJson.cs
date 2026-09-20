using System.Text.Json.Serialization;

namespace Janus.Hosting.Accounts;

/// <summary>
/// How the well-known documents are written, generated rather than reflected over
/// (CONV-CODE-004, CONV-DESIGN-006).
/// </summary>
/// <remarks>Implements REG-PM-001.</remarks>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(PasskeyEndpointsView))]
internal sealed partial class WellKnownJson : JsonSerializerContext;
