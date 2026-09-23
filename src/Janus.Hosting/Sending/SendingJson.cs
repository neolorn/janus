using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Janus.Hosting.Sending;

/// <summary>
/// How the administration of sending reads and writes, generated rather than reflected
/// over (CONV-CODE-004, CONV-DESIGN-006).
/// </summary>
/// <remarks>Implements chapter 09 section 8 and CONV-DESIGN-006.</remarks>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UseStringEnumConverter = true)]
[JsonSerializable(typeof(RestrictionBody))]
[JsonSerializable(typeof(RestrictionDeletionBody))]
[JsonSerializable(typeof(RestrictionGrantBody))]
[JsonSerializable(typeof(RestrictionView))]
[JsonSerializable(typeof(IReadOnlyList<RestrictionView>))]
internal sealed partial class SendingJson : JsonSerializerContext;
