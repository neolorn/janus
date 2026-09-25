using System.Text.Json;

namespace Janus.Hosting.Configuration;

/// <summary>
/// What changing one key carries, as the request reads.
/// </summary>
/// <param name="Value">What the key becomes, in its own type.</param>
/// <param name="Reason">Why, which every change carries.</param>
/// <remarks>Implements chapter 09 section 8, OPS-CFG-005 and D-147.</remarks>
internal sealed record ConfigurationBody(JsonElement? Value, string? Reason);
