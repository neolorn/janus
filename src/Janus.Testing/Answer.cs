using System;
using System.Net;
using System.Text.Json;

namespace Janus.Testing;

/// <summary>
/// What one of the library's endpoints answered: its status, and its body read as JSON.
/// </summary>
/// <param name="Status">The status.</param>
/// <param name="Body">The body, or the undefined element where there was none.</param>
/// <remarks>
/// Implements LIB-TEST-003.
/// </remarks>
internal sealed record Answer(HttpStatusCode Status, JsonElement Body)
{
    /// <summary>
    /// The code a refusal carries (API-CONV-002), or nothing where the answer is no
    /// refusal.
    /// </summary>
    public string? Code =>
        Body.ValueKind is JsonValueKind.Object
            && Body.TryGetProperty("code", out JsonElement code)
            && code.ValueKind is JsonValueKind.String
                ? code.GetString()
                : null;

    /// <summary>
    /// A member of the body that holds text.
    /// </summary>
    /// <param name="name">The member's name.</param>
    /// <returns>The text.</returns>
    /// <exception cref="InvalidOperationException">The member holds no text.</exception>
    public string Text(string name) =>
        Body.GetProperty(name).GetString()
            ?? throw new InvalidOperationException("The answer's " + name + " holds no text.");
}
