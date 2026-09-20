using System.Text.Json.Serialization;

namespace Janus.Hosting.Registration;

/// <summary>
/// How registration reads and writes, generated rather than reflected over
/// (CONV-CODE-004, CONV-DESIGN-006).
/// </summary>
/// <remarks>Implements API-CONV-002 and CONV-DESIGN-006.</remarks>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(BeginRegistrationRequest))]
[JsonSerializable(typeof(AgeRequest))]
[JsonSerializable(typeof(IdentifierValueRequest))]
[JsonSerializable(typeof(AddIdentifierRequest))]
[JsonSerializable(typeof(SecurityRequest))]
[JsonSerializable(typeof(TermsRequest))]
[JsonSerializable(typeof(VerifyRequest))]
[JsonSerializable(typeof(AbandonRequest))]
[JsonSerializable(typeof(RegistrationStateView))]
[JsonSerializable(typeof(LinkLandingView))]
internal sealed partial class RegistrationJson : JsonSerializerContext;
