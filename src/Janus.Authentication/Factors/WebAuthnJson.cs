using System.Text.Json.Serialization;

namespace Janus.Authentication.Factors;

/// <summary>
/// How the related-origins document is written, generated rather than reflected over
/// (CONV-CODE-004).
/// </summary>
/// <remarks>Implements AUTH-FACT-012.</remarks>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(RelatedOrigins))]
internal sealed partial class WebAuthnJson : JsonSerializerContext;
