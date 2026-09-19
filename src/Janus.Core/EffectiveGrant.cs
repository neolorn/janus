using System;

namespace Janus.Core;

/// <summary>
/// One row of what the grants in force allow: a grant paired with one permission of the
/// role it confers. A grant naming a role of six permissions is six rows, and editing
/// the role changes them at once, because nothing here is stored twice.
/// </summary>
/// <param name="GrantId">The grant the row comes from, which an explanation names.</param>
/// <param name="SubjectType">Whether the holder is an account or a group.</param>
/// <param name="SubjectId">The account or the group holding it.</param>
/// <param name="Role">The role it confers, which an explanation names.</param>
/// <param name="Permission">One permission the role confers.</param>
/// <param name="ResourceType">The kind of thing it is on, absent for the organization.</param>
/// <param name="ResourceId">The record it is on, absent for the organization.</param>
/// <param name="Deny">Whether it takes access away rather than conferring it.</param>
/// <param name="Kind">Whether it was written, derived, or precomputed from a derivation.</param>
/// <param name="Organization">The organization it is scoped to.</param>
/// <param name="ExpiresAt">When it stops conferring anything, where it expires.</param>
/// <param name="RevokedAt">When it was taken back, where it was.</param>
/// <remarks>
/// Implements AUTHZ-GRANT-001, AUTHZ-CACHE-001 and LIB-API-001. Expiry and revocation
/// are carried rather than applied, so the instant a question is asked at decides the
/// answer and no sweep is involved. The columns are plain values rather than the
/// library's own types, because what maps this row is the host's provider and not the
/// library's (D-159).
/// </remarks>
public sealed record EffectiveGrant(
    Guid GrantId,
    string SubjectType,
    Guid SubjectId,
    string Role,
    string Permission,
    string? ResourceType,
    string? ResourceId,
    bool Deny,
    string Kind,
    Guid Organization,
    DateTimeOffset? ExpiresAt,
    DateTimeOffset? RevokedAt);
