using System;
using System.Collections.Generic;
using System.Text.Json;

namespace Janus.Core;

/// <summary>
/// A condition of OPS-ALERT-001 fired.
/// </summary>
/// <param name="RaisedAt">When the condition fired.</param>
/// <param name="IdempotencyKey">
/// The key a consumer recognises a repeat by, which is the condition and what it
/// fired about inside the deduplication window (OPS-ALERT-002).
/// </param>
/// <param name="Condition">Which condition, as chapter 10 section 5.23 names it.</param>
/// <param name="Severity">How urgent it is.</param>
/// <param name="Details">The structured context of the row, never a sentence.</param>
/// <remarks>Implements OPS-ALERT-001, OPS-ALERT-002, chapter 10 sections 5.23 and 5b.</remarks>
public sealed record AlertRaised(
    DateTimeOffset RaisedAt,
    string IdempotencyKey,
    AlertCondition Condition,
    AlertSeverity Severity,
    IReadOnlyDictionary<string, JsonElement> Details) : DomainEvent(RaisedAt, IdempotencyKey);
