using System.Collections.Generic;
using System.Text.Json;
using Janus.Core;

namespace Janus.Privacy.Tests;

/// <summary>
/// One condition the area raised.
/// </summary>
/// <param name="Condition">Which condition.</param>
/// <param name="Scope">Whose, or nothing where the row names no one.</param>
/// <param name="Details">The structured context.</param>
internal sealed record PrivacyAlertRaised(
    AlertCondition Condition,
    string? Scope,
    IReadOnlyDictionary<string, JsonElement> Details);
