using System;
using System.Collections.Generic;
using System.Linq;

namespace Janus.Authorization.Gate;

/// <summary>
/// One record of a page that one derivation admits, as the host's own query answers it.
/// </summary>
/// <param name="Resource">The record.</param>
/// <param name="Ancestor">
/// The container the relationship names, which is the record itself where the
/// derivation sits on its own type.
/// </param>
/// <param name="Relationship">The relationship whose derivation admitted it.</param>
/// <param name="Depth">
/// How far above the record the container sits, nothing between them being zero.
/// </param>
/// <remarks>
/// Implements AUTHZ-DERIVE-001, AUTHZ-GATE-004 and AUTHZ-GATE-005 AC1. The relationship
/// travels with the record so that what its derivation confers is mapped where the
/// model is, rather than asked for in a query of its own per permission, and the
/// container travels with it so that an explanation can name what the access was
/// inherited from.
/// </remarks>
internal sealed record AdmittedRecord(string Resource, string Ancestor, string Relationship, int Depth)
{
    /// <summary>
    /// The row an explanation names where several admit the record: the nearest
    /// container first, as the stored grants are ordered, then the relationship and the
    /// container by name, so the same rows always name the same one.
    /// </summary>
    /// <param name="admitted">The rows that admit one record.</param>
    /// <returns>The row, or nothing where none admits it.</returns>
    /// <exception cref="ArgumentNullException">The rows are absent.</exception>
    /// <remarks>Implements AUTHZ-GATE-004 and D-166.</remarks>
    public static AdmittedRecord? Nearest(IEnumerable<AdmittedRecord> admitted)
    {
        ArgumentNullException.ThrowIfNull(admitted);

        return admitted
            .OrderBy(record => record.Depth)
            .ThenBy(record => record.Relationship, StringComparer.Ordinal)
            .ThenBy(record => record.Ancestor, StringComparer.Ordinal)
            .FirstOrDefault();
    }
}
