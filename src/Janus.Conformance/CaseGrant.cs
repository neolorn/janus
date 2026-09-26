using Janus.Core;

namespace Janus.Conformance;

/// <summary>
/// One grant a truth-table case writes.
/// </summary>
/// <param name="Subject">Who holds it.</param>
/// <param name="Role">The role it confers.</param>
/// <param name="Organization">The organization it is recorded in.</param>
/// <param name="On">The record it is on, or nothing for the whole organization.</param>
/// <param name="Granter">The account it is recorded as granted by.</param>
/// <remarks>Implements AUTHZ-TEST-001 AC1.</remarks>
internal sealed record CaseGrant(
    GrantSubject Subject,
    RoleName Role,
    OrganizationId Organization,
    ResourceReference? On,
    SubjectId Granter)
{
    /// <summary>
    /// Whether it takes access away rather than conferring it.
    /// </summary>
    public bool Deny { get; init; }

    /// <summary>
    /// Whether it stopped conferring anything an hour ago.
    /// </summary>
    public bool Expired { get; init; }

    /// <summary>
    /// Whether it was taken back an hour ago.
    /// </summary>
    public bool Revoked { get; init; }
}
