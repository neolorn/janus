using System;
using System.Collections.Generic;
using System.Text.Json;

namespace Janus.Hosting.Tests;

/// <summary>
/// What one request came back with.
/// </summary>
/// <param name="Status">The status code.</param>
/// <param name="Body">What the response carried, empty where it carried nothing.</param>
/// <param name="Location">Where a redirect pointed, or nothing.</param>
/// <param name="SetCookie">The cookies the answer wrote.</param>
internal sealed record Answer(
    int Status,
    string Body,
    string? Location,
    IReadOnlyList<string> SetCookie)
{
    /// <summary>
    /// The response headers, by name.
    /// </summary>
    public IReadOnlyDictionary<string, string> Headers { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// One response header.
    /// </summary>
    /// <param name="name">Which header.</param>
    /// <returns>Its value, or nothing where the answer carried none.</returns>
    public string? Header(string name) =>
        Headers.TryGetValue(name, out string? value) ? value : null;

    /// <summary>
    /// The body read as JSON.
    /// </summary>
    /// <returns>The document.</returns>
    public JsonElement Json() => JsonDocument.Parse(Body).RootElement;

    /// <summary>
    /// One property of the body.
    /// </summary>
    /// <param name="name">Which property.</param>
    /// <returns>Its text.</returns>
    public string Text(string name) => Json().GetProperty(name).GetString() ?? string.Empty;
}
