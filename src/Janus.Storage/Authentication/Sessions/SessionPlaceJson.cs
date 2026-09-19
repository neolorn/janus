using System.Text.Json.Serialization;

namespace Janus.Storage.Authentication.Sessions;

/// <summary>
/// How the encrypted place is written and read, generated rather than reflected over
/// (CONV-CODE-004).
/// </summary>
/// <remarks>Implements AUTH-SESS-013.</remarks>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(SessionPlace))]
internal sealed partial class SessionPlaceJson : JsonSerializerContext;
