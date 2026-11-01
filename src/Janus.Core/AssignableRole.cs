namespace Janus.Core;

/// <summary>
/// A role a caller may give in an organization: its name, and whether every grant of it
/// must carry an end.
/// </summary>
/// <param name="Name">The role.</param>
/// <param name="GrantsMustEnd">Whether a grant of it is written only with an expiry (AUTHZ-GRANT-003).</param>
/// <remarks>Implements AUTHZ-GRANT-005 and LIB-API-005 (D-247).</remarks>
public sealed record AssignableRole(RoleName Name, bool GrantsMustEnd);
