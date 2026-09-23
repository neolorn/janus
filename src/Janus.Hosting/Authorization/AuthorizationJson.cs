using System.Text.Json.Serialization;

namespace Janus.Hosting.Authorization;

/// <summary>
/// How the administration of access reads and writes, generated rather than reflected
/// over (CONV-CODE-004, CONV-DESIGN-006).
/// </summary>
/// <remarks>Implements AUTHZ-GATE-004, AUTHZ-GRANT-001, AUTHZ-GRANT-003 and API-CONV-002.</remarks>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UseStringEnumConverter = true)]
[JsonSerializable(typeof(CreatedGrantView))]
[JsonSerializable(typeof(ExplanationView))]
[JsonSerializable(typeof(GrantBody))]
[JsonSerializable(typeof(GrantRevocationBody))]
internal sealed partial class AuthorizationJson : JsonSerializerContext;
