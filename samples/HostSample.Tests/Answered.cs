using System.Net;
using System.Text.Json;

namespace HostSample.Tests;

/// <summary>
/// What the application answered a request: its status, and its body as JSON, or
/// nothing where it wrote none.
/// </summary>
/// <param name="Status">The status.</param>
/// <param name="Body">The body.</param>
internal sealed record Answered(HttpStatusCode Status, JsonElement? Body)
{
    /// <summary>
    /// The code a refusal carries, or nothing where the answer carries none.
    /// </summary>
    public string? Code =>
        Body is { ValueKind: JsonValueKind.Object } body && body.TryGetProperty("code", out JsonElement code)
            ? code.GetString()
            : null;
}
