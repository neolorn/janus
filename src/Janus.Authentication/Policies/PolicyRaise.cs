using System;
using Janus.Core;

namespace Janus.Authentication.Policies;

/// <summary>
/// A requirement a policy raised, and the instant it was raised at.
/// </summary>
/// <param name="Field">Which field.</param>
/// <param name="Value">What it was raised to.</param>
/// <param name="At">When.</param>
/// <remarks>Implements AUTH-FACT-017.</remarks>
internal sealed record PolicyRaise(PolicyField Field, string Value, DateTimeOffset At);
