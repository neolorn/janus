using System.Text.Json.Serialization;

namespace Janus.Hosting.Maintenance;

/// <summary>
/// How the licence and maintenance log endpoints read and write, generated rather
/// than reflected over (CONV-CODE-004, CONV-DESIGN-006).
/// </summary>
/// <remarks>Implements API-CONV-002 and CONV-DESIGN-006.</remarks>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(LicencesBody))]
[JsonSerializable(typeof(LicencesView))]
[JsonSerializable(typeof(MaintenanceEntryBody))]
[JsonSerializable(typeof(MaintenanceEntryView))]
[JsonSerializable(typeof(MaintenanceLogView))]
internal sealed partial class MaintenanceJson : JsonSerializerContext;
