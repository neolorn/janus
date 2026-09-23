using Janus.Core;

namespace Janus.Storage.Authorization.Resources;

/// <summary>
/// The <c>resources</c> row: one of the host's records as the library knows it.
/// </summary>
/// <remarks>
/// Implements AUTHZ-INHERIT-001, AUTHZ-SCOPE-001 and CONV-DESIGN-003. The library never
/// reads the host's table, so the host says this when it creates or moves the record,
/// and the ancestry beside it is written in the same transaction.
/// </remarks>
internal sealed class ResourceRecord
{
    /// <summary>
    /// The <c>resource_type</c> column.
    /// </summary>
    public ResourceType Type { get; set; }

    /// <summary>
    /// The <c>resource_id</c> column.
    /// </summary>
    public ResourceId Id { get; set; }

    /// <summary>
    /// The <c>organization</c> column.
    /// </summary>
    public OrganizationId Organization { get; set; }

    /// <summary>
    /// The <c>subject</c> column: the data subject of the record, as the column the
    /// type declares for its encrypted fields holds it, absent where the record is
    /// about nobody (PRIV-RIGHT-005a, PRIV-SENS-002).
    /// </summary>
    public SubjectId? Subject { get; set; }

    /// <summary>
    /// The <c>contained_in_type</c> column, absent where nothing contains the record.
    /// </summary>
    public ResourceType? ContainedInType { get; set; }

    /// <summary>
    /// The <c>contained_in_id</c> column, absent where nothing contains the record.
    /// </summary>
    public ResourceId? ContainedInId { get; set; }
}
