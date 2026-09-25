namespace Janus.Hosting.Authorization;

/// <summary>
/// What removing a role carries, as the request reads.
/// </summary>
/// <param name="Reason">Why, which every change to a role records.</param>
/// <remarks>Implements chapter 09 section 8 and AUTHZ-GRANT-004.</remarks>
internal sealed record RoleRemovalBody(string? Reason);
