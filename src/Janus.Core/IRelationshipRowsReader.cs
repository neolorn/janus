using System.Linq;

namespace Janus.Core;

/// <summary>
/// What composes a query over the rows of a declared relationship, given them under the
/// host's own row type.
/// </summary>
/// <remarks>
/// Implements LIB-HOST-001 and CONV-CODE-004 (D-183). The row type is known where the
/// host declares the source, so a reader is handed the rows through a generic method
/// and nothing reads the type at runtime.
/// </remarks>
internal interface IRelationshipRowsReader
{
    /// <summary>
    /// Takes the rows of the relationship, from the host's own context.
    /// </summary>
    /// <typeparam name="TRow">The host's relationship row.</typeparam>
    /// <param name="rows">The rows, as the host's context reads them.</param>
    void Read<TRow>(IQueryable<TRow> rows);
}
