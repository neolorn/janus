using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace Janus.Core;

/// <summary>
/// The names of the step-up actions the library once had and removed, kept beside the
/// current ones, so that a gate a stored policy still names for one is told apart from
/// a damaged row.
/// </summary>
/// <remarks>Implements chapter 10 section 5a (D-235).</remarks>
internal static class RetiredStepUpActions
{
    /// <summary>
    /// Gets the retired names: <c>alerting:destinations</c>, since alert destinations
    /// are the host's (D-225).
    /// </summary>
    public static FrozenSet<string> Names { get; } =
        new[] { "alerting:destinations" }.ToFrozenSet(StringComparer.Ordinal);

    /// <summary>
    /// The retired actions a stored policy or override names a gate for, in the order
    /// it names them (D-238).
    /// </summary>
    /// <param name="stored">The policy as the settings table holds it.</param>
    /// <returns>The retired names, none where it names none or holds no gates.</returns>
    /// <exception cref="JsonException">The text is not JSON, which no write stores.</exception>
    public static IReadOnlyList<string> NamedIn(string stored)
    {
        using var written = JsonDocument.Parse(stored);

        return written.RootElement.ValueKind is JsonValueKind.Object
            && written.RootElement.TryGetProperty("gates", out JsonElement gates)
            && gates.ValueKind is JsonValueKind.Object
                ? [.. gates.EnumerateObject().Select(gate => gate.Name).Where(Names.Contains)]
                : [];
    }
}
