namespace Janus.Core;

/// <summary>
/// Who the evaluation was made for: whose credentials the request was made under, and
/// whose identity the action was taken under.
/// </summary>
/// <param name="Acting">Whose credentials, or nothing where a system principal asked.</param>
/// <param name="Effective">Whose identity, or nothing where a system principal asked.</param>
/// <remarks>Implements AUTHZ-GATE-004 and AUTHZ-IMP-001.</remarks>
public readonly record struct ExplainedPrincipal(SubjectId? Acting, SubjectId? Effective);
