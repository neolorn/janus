using System.Text.Json.Serialization;

namespace Janus.Hosting.Search;

/// <summary>
/// How the staff search reads and writes, generated rather than reflected over
/// (CONV-CODE-004, CONV-DESIGN-006).
/// </summary>
/// <remarks>Implements API-CONV-002 and CONV-DESIGN-006 (D-256).</remarks>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UseStringEnumConverter = true)]
[JsonSerializable(typeof(SearchRequest))]
[JsonSerializable(typeof(SearchResultsView))]
internal sealed partial class SearchJson : JsonSerializerContext;
