using System.Text.Json.Serialization;

namespace Janus.Hosting.Privacy;

/// <summary>
/// How the privacy documents are written, generated rather than reflected over
/// (CONV-CODE-004, CONV-DESIGN-006).
/// </summary>
/// <remarks>Implements PRIV-CONS-005 and PRIV-CONS-006.</remarks>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(DocumentVersionView))]
internal sealed partial class PrivacyJson : JsonSerializerContext;
