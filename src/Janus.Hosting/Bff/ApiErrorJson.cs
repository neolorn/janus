using System.Text.Json.Serialization;

namespace Janus.Hosting.Bff;

/// <summary>
/// How an error is written, generated rather than reflected over (CONV-CODE-004).
/// </summary>
/// <remarks>Implements API-CONV-002.</remarks>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(ApiError))]
internal sealed partial class ApiErrorJson : JsonSerializerContext;
