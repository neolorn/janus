using System;

namespace Janus.Core;

/// <summary>
/// One sentence to be written down: a subject has a role on a resource, or is denied
/// it there.
/// </summary>
/// <param name="Subject">Who holds it: one account, or a group whose members hold it transitively.</param>
/// <param name="Role">The role it confers or denies.</param>
/// <param name="On">
/// The record it is on, or the type <c>organization</c> and the organization's
/// identifier where it is on the whole organization.
/// </param>
/// <param name="Deny">Whether it takes access away rather than conferring it.</param>
/// <param name="ExpiresAt">When it stops conferring anything, where it expires.</param>
/// <param name="Reason">Why, which every grant records.</param>
/// <remarks>Implements AUTHZ-GRANT-001, AUTHZ-GRANT-002, AUTHZ-GRANT-003 and chapter 09 section 8.</remarks>
public sealed record GrantRequest(
    GrantSubject Subject,
    RoleName Role,
    ResourceReference On,
    bool Deny,
    DateTimeOffset? ExpiresAt,
    string Reason);
