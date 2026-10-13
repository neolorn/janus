using System;

namespace Janus.Authorization.Gate;

/// <summary>
/// One row that reaches the record a single check asks about, as the host's own query
/// answers it: a stored grant the rule matches, or a derivation that admits the record.
/// </summary>
/// <remarks>
/// Implements AUTHZ-DERIVE-001 and AUTHZ-GATE-004 (D-166). The rows come ordered as the
/// stored candidates are: a deny first, then the nearest container, then by grant.
/// </remarks>
internal sealed class CandidateRow
{
    /// <summary>
    /// The stored grant, or nothing for a derivation.
    /// </summary>
    public Guid? Grant { get; init; }

    /// <summary>
    /// How the grant came to be, in the spelling its column holds.
    /// </summary>
    public string? Kind { get; init; }

    /// <summary>
    /// Whether an account or a group holds the grant, in the spelling its column holds.
    /// </summary>
    public string? SubjectType { get; init; }

    /// <summary>
    /// The account or the group holding the grant.
    /// </summary>
    public Guid? SubjectId { get; init; }

    /// <summary>
    /// The role the grant confers.
    /// </summary>
    public string? Role { get; init; }

    /// <summary>
    /// Whether the grant takes the permission away.
    /// </summary>
    public bool Deny { get; init; }

    /// <summary>
    /// The kind of thing the grant or the relationship sits on, and nothing where the
    /// grant sits on the organization.
    /// </summary>
    public string? AncestorType { get; init; }

    /// <summary>
    /// The record it sits on, and nothing where the grant sits on the organization.
    /// </summary>
    public string? AncestorId { get; init; }

    /// <summary>
    /// How far above the record that is, and nothing where the grant sits on the
    /// organization.
    /// </summary>
    public int? Depth { get; init; }

    /// <summary>
    /// The relationship whose derivation admits the record, for a derivation.
    /// </summary>
    public string? Relationship { get; init; }
}
