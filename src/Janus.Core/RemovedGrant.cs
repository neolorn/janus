namespace Janus.Core;

/// <summary>
/// An allow grant a removal of access takes away.
/// </summary>
/// <param name="Grant">The grant.</param>
/// <param name="Role">Its role.</param>
/// <param name="Organization">
/// The organization it is in, which is the removal's own save where the end of a membership
/// of the administrative organization reaches the grants of another.
/// </param>
/// <remarks>Implements AUTHZ-GRANT-005 (D-281).</remarks>
public sealed record RemovedGrant(GrantId Grant, RoleName Role, OrganizationId Organization);
