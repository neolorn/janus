using System.Text.Json.Serialization;

namespace Janus.Hosting.Authorization;

/// <summary>
/// How the administration of access is written, generated rather than reflected over
/// (CONV-CODE-004, CONV-DESIGN-006).
/// </summary>
/// <remarks>Implements AUTHZ-GATE-004 and API-CONV-002.</remarks>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UseStringEnumConverter = true)]
[JsonSerializable(typeof(ExplanationView))]
internal sealed partial class AuthorizationJson : JsonSerializerContext;
