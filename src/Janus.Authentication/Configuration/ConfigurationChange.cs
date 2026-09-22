using System;
using Janus.Core;
using Janus.Core.Configuration;

namespace Janus.Authentication.Configuration;

/// <summary>
/// One runtime setting change as the trail holds it.
/// </summary>
/// <param name="Key">Which setting.</param>
/// <param name="Before">What it read as, in the form the settings table writes.</param>
/// <param name="After">What it reads as now, in the same form.</param>
/// <param name="Loosening">Whether the change loosens the deployment.</param>
/// <param name="Reason">The written reason, which a loosening requires.</param>
/// <param name="Actor">Who made it.</param>
/// <param name="At">When it was made.</param>
/// <remarks>
/// Implements OPS-CFG-002 and OPS-CFG-005: who, what, from, to, when and why, with the
/// values written exactly as the settings table writes them, so an entry reads the same
/// way as the row the change produced.
/// </remarks>
internal sealed record ConfigurationChange(
    ConfigurationKey Key,
    string Before,
    string After,
    bool Loosening,
    string? Reason,
    SubjectId Actor,
    DateTimeOffset At);
