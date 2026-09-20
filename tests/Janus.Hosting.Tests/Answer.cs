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
