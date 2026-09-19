using System;
using Janus.Core;

namespace Janus.Authorization.Gate;

/// <summary>
/// One grant the rule matched on one record, with the container it reached the record
/// through.
/// </summary>
/// <remarks>
/// Implements AUTHZ-GATE-004. The columns are what an explanation names, read in the
/// one query that decides the outcome rather than in a second one asked afterwards.
/// </remarks>
internal sealed class CandidateGrant
{
    /// <summary>
    /// Which grant.
    /// </summary>
    public Guid Grant { get; init; }

    /// <summary>
    /// Whether someone wrote it, a fact produced it, or it was precomputed.
    /// </summary>
    public GrantKind Kind { get; init; }

    /// <summary>
    /// Whether it is held by an account or by a group.
    /// </summary>
    public SubjectType SubjectType { get; init; }

    /// <summary>
    /// The account or the group holding it.
    /// </summary>
    public Guid SubjectId { get; init; }

    /// <summary>
    /// The role it confers.
    /// </summary>
    public RoleName Role { get; init; }

    /// <summary>
    /// Whether it took the permission away rather than conferring it.
    /// </summary>
    public bool Deny { get; init; }

    /// <summary>
    /// The kind of thing it sits on, and nothing where it sits on the organization.
    /// </summary>
    public string? AncestorType { get; init; }

    /// <summary>
    /// The record it sits on, and nothing where it sits on the organization.
    /// </summary>
    public string? AncestorId { get; init; }
}
