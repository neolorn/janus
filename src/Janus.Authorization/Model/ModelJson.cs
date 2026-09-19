using System.Text.Json;
using System.Text.Json.Serialization;

namespace Janus.Authorization.Model;

/// <summary>
/// How the built model is written, generated rather than reflected over
/// (CONV-CODE-004, CONV-DESIGN-003).
/// </summary>
/// <remarks>Implements AUTHZ-MODEL-005.</remarks>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = true,
    NewLine = "\n",
    DefaultIgnoreCondition = JsonIgnoreCondition.Never)]
[JsonSerializable(typeof(SerializedModel))]
internal sealed partial class ModelJson : JsonSerializerContext;
