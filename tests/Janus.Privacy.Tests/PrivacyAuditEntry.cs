using System;
using System.Collections.Generic;
using System.Text.Json;
using Janus.Core;

namespace Janus.Privacy.Tests;

/// <summary>
/// One entry the area wrote to the audit trail.
/// </summary>
/// <param name="Action">What happened.</param>
/// <param name="Acting">Who did it.</param>
/// <param name="Subject">Whose account it was done on.</param>
/// <param name="At">When.</param>
/// <param name="Details">The structured context.</param>
/// <param name="Principal">The system principal that did it, where background work did.</param>
internal sealed record PrivacyAuditEntry(
    AuditAction Action,
    SubjectId? Acting,
    SubjectId? Subject,
    DateTimeOffset At,
    IReadOnlyDictionary<string, JsonElement> Details,
    SystemPrincipal? Principal = null)
{
    /// <summary>
    /// The reason given at the use of the break-glass credential, where the change was
    /// made in the session it opened, or nothing.
    /// </summary>
    public string? BreakGlassReason { get; init; }

    /// <summary>
    /// The organization the entry is filed under, where it names one.
    /// </summary>
    public OrganizationId? Organization { get; init; }
}
