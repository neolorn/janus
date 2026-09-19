using System;

namespace Janus.Core;

/// <summary>
/// A requirement an account does not yet meet, with the instant its run-up ends.
/// </summary>
/// <param name="Field">Which field was raised.</param>
/// <param name="Value">What it was raised to.</param>
/// <param name="Deadline">When the run-up ends.</param>
/// <remarks>
/// Implements AUTH-FACT-017 and AUTH-SESS-009. The sentence the person reads is the
/// frontend's; what crosses the boundary is the field, the value and the instant
/// (CONV-CONTENT-001).
/// </remarks>
public sealed record PolicyRequirement(PolicyField Field, string Value, DateTimeOffset Deadline);
