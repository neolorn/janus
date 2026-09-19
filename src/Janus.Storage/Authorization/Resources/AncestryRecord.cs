using Janus.Core;

namespace Janus.Storage.Authorization.Resources;

/// <summary>
/// The <c>ancestry</c> row: a record, one of the things containing it, and the remove.
/// </summary>
/// <remarks>
/// Implements AUTHZ-INHERIT-002 and LIB-API-001. The table is public contract, so its
/// columns are what <see cref="AncestryEntry"/> gives and its shape cannot change
/// without a major version. A record is its own ancestor at depth zero, which is what
/// lets one predicate answer a grant on the record and a grant on its container.
/// </remarks>
internal sealed class AncestryRecord
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
    /// The <c>ancestor_type</c> column.
    /// </summary>
    public ResourceType AncestorType { get; set; }

    /// <summary>
    /// The <c>ancestor_id</c> column.
    /// </summary>
    public ResourceId AncestorId { get; set; }

    /// <summary>
    /// The <c>depth</c> column, zero where the ancestor is the record itself.
    /// </summary>
    public int Depth { get; set; }

    /// <summary>
    /// The <c>organization</c> column, the one owning the record.
    /// </summary>
    public OrganizationId Organization { get; set; }
}
