namespace Janus.Hosting.Authorization;

/// <summary>
/// What revoking a grant carries, as the request reads.
/// </summary>
/// <param name="Reason">Why, which every revocation records.</param>
/// <remarks>Implements chapter 09 section 8 and AUTHZ-GRANT-003.</remarks>
internal sealed record GrantRevocationBody(string? Reason);
