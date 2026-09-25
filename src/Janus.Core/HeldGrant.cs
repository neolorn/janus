using System;

namespace Janus.Core;

/// <summary>
/// A live grant one user or group holds in its own name, with who wrote it, when and
/// why.
/// </summary>
/// <param name="Id">The grant's identifier, which a revocation names.</param>
/// <param name="Kind">Whether somebody wrote it or a materialisation did.</param>
/// <param name="Subject">Who holds it.</param>
/// <param name="Role">The role it confers or denies.</param>
/// <param name="On">
/// The record it is on, or the type <c>organization</c> and the organization's
/// identifier where it is on the whole organization, as a grant is written.
/// </param>
/// <param name="Deny">Whether it takes access away rather than conferring it.</param>
/// <param name="ExpiresAt">When it stops conferring anything, where it expires.</param>
/// <param name="GrantedBy">Who wrote it.</param>
/// <param name="GrantedAt">When it was written.</param>
/// <param name="Reason">Why it was written.</param>
/// <remarks>
/// Implements AUTHZ-GRANT-003 AC3 and chapter 16 section 3 step 4 (entry 268): what a
/// departing person holds directly is read before it is removed or transferred.
/// </remarks>
public sealed record HeldGrant(
    GrantId Id,
    GrantKind Kind,
    GrantSubject Subject,
    RoleName Role,
    ResourceReference On,
    bool Deny,
    DateTimeOffset? ExpiresAt,
    SubjectId GrantedBy,
    DateTimeOffset GrantedAt,
    string Reason);
