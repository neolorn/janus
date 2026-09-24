using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Janus.Storage.Authentication.Sending;

/// <summary>
/// How an undelivered message is written and read, generated rather than reflected
/// over (CONV-CODE-004).
/// </summary>
/// <remarks>Implements D-022.</remarks>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(SendDeliveryDocument))]
[JsonSerializable(typeof(List<string>))]
internal sealed partial class SendDeliveryJson : JsonSerializerContext;
