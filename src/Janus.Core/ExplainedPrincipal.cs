namespace Janus.Core;

/// <summary>
/// Who the evaluation was made for: whose credentials the request was made under, and
/// whose identity the action was taken under, or the system principal that asked.
/// </summary>
/// <param name="Acting">Whose credentials, or nothing where a system principal asked.</param>
/// <param name="Effective">Whose identity, or nothing where a system principal asked.</param>
/// <param name="Name">The system principal's name, or nothing where a person asked.</param>
/// <param name="Reason">The reason that principal stated, or nothing where a person asked.</param>
/// <remarks>
/// Implements AUTHZ-GATE-004, AUTHZ-CONCEAL-004 and AUTHZ-IMP-001. The name and the
/// reason are present only for a system principal (D-166).
/// </remarks>
public readonly record struct ExplainedPrincipal(
    SubjectId? Acting,
    SubjectId? Effective,
    string? Name,
    string? Reason);
