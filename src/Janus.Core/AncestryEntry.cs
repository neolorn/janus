using System;

namespace Janus.Core;

/// <summary>
/// One row of the ancestry closure: a record of the host's, one of the things
/// containing it, and how far above it that container sits. A record is its own
/// ancestor at depth zero, so every registered record has at least one row.
/// </summary>
/// <param name="ResourceType">The kind of thing the row is about.</param>
/// <param name="ResourceId">The record, as the host names it.</param>
/// <param name="AncestorType">The kind of thing containing it.</param>
/// <param name="AncestorId">The container, as the host names it.</param>
/// <param name="Depth">How far above the record the container sits, zero being itself.</param>
/// <param name="Organization">The organization owning the record.</param>
/// <remarks>
/// Implements AUTHZ-INHERIT-002 and LIB-API-001. The closure is public contract and
/// cannot be restructured without a major version: a host maps it into its own context
/// with <c>MapAuthorizationTables</c>, and hand-written SQL reaches it through the gate's
/// SQL fragment. The columns are plain values rather than the library's own types,
/// because what maps this row is the host's provider and not the library's (D-159).
/// A row is an input to the gate's renderings, not a permission: a permission is asked
/// only through the gate, and what a row confers is the library's to decide, which a
/// later version may narrow (AUTHZ-SEAM-001, D-269).
/// </remarks>
public sealed record AncestryEntry(
    string ResourceType,
    string ResourceId,
    string AncestorType,
    string AncestorId,
    int Depth,
    Guid Organization);
