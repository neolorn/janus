using Janus.Core;

namespace Janus.Storage.Authorization.Groups;

/// <summary>
/// The <c>groups</c> row.
/// </summary>
/// <remarks>
/// Implements AUTHZ-GROUP-001 and CONV-DESIGN-003.
/// </remarks>
internal sealed class GroupRecord
{
    /// <summary>
    /// The <c>id</c> column, which is this table's key.
    /// </summary>
    public GroupId Id { get; set; }

    /// <summary>
    /// The <c>organization</c> column.
    /// </summary>
    public OrganizationId Organization { get; set; }

    /// <summary>
    /// The <c>name</c> column.
    /// </summary>
    public string Name { get; set; } = string.Empty;
}
