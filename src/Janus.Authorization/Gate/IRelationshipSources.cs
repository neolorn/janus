using System.Collections.Generic;
using Janus.Core;

namespace Janus.Authorization.Gate;

/// <summary>
/// The rows of the declared relationships, read through the sources the host declared
/// for them, each reading one statement in the host's own context.
/// </summary>
/// <remarks>
/// Implements AUTHZ-DERIVE-005, AUTHZ-DERIVE-007, LIB-HOST-001 and LIB-HOST-002 (D-166,
/// D-183). The library queries no host table of its own: the scope gives one instance of
/// the host's context, and the rows and the ancestry closure that instance maps are
/// composed into one query the host's context runs. This port is what the "who can
/// access this?" view reads through; the check and the filter read the rows the host
/// passes them (AUTHZ-DERIVE-001).
/// </remarks>
internal interface IRelationshipSources
{
    /// <summary>
    /// Whether the host declared a source for a relationship.
    /// </summary>
    /// <param name="relationship">The relationship, as the model declares it.</param>
    /// <returns>Whether its rows can be read.</returns>
    bool Declares(string relationship);

    /// <summary>
    /// Who holds a relationship on a record or on anything containing it of the type
    /// the relationship is declared on, in one statement.
    /// </summary>
    /// <param name="relationship">The relationship.</param>
    /// <param name="resource">The record asked about.</param>
    /// <returns>
    /// Each holder with the record its row names, those on the record itself first and
    /// then those on each container nearest first.
    /// </returns>
    /// <exception cref="System.InvalidOperationException">No source is declared for the relationship.</exception>
    IAsyncEnumerable<HeldRelationship> HeldAbove(
        RelationshipDeclaration relationship,
        ResourceReference resource);
}
