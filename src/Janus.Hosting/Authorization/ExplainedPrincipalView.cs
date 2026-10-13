using System;
using System.Text.Json.Serialization;

namespace Janus.Hosting.Authorization;

/// <summary>
/// Whose credentials a request was made under and whose identity it acted under, or
/// the system principal that asked.
/// </summary>
/// <param name="Acting">Whose credentials, or nothing where a system principal asked.</param>
/// <param name="Effective">Whose identity, or nothing where a system principal asked.</param>
/// <param name="Name">The system principal's name, present only where one asked.</param>
/// <param name="Reason">The reason that principal stated, present only where one asked.</param>
/// <remarks>Implements AUTHZ-GATE-004, AUTHZ-CONCEAL-004 and AUTHZ-IMP-001.</remarks>
internal sealed record ExplainedPrincipalView(
    Guid? Acting,
    Guid? Effective,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Name,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Reason);
