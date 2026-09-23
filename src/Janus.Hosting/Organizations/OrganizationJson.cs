using System.Text.Json.Serialization;

namespace Janus.Hosting.Organizations;

/// <summary>
/// How the administration of organizations reads and writes, generated rather than
/// reflected over (CONV-CODE-004, CONV-DESIGN-006).
/// </summary>
/// <remarks>Implements IDN-ORG-002, IDN-ORG-003 and API-CONV-002.</remarks>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UseStringEnumConverter = true)]
[JsonSerializable(typeof(CreatedOrganizationView))]
[JsonSerializable(typeof(OrganizationBody))]
[JsonSerializable(typeof(OrganizationReasonBody))]
internal sealed partial class OrganizationJson : JsonSerializerContext;
