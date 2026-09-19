using System.Collections.Generic;
using System.Linq.Expressions;

namespace Janus.Core;

/// <summary>
/// One fact in the host's own data that a derivation may follow from: a row of one of
/// the host's relations naming a subject and one of its records. The library never
/// queries the relation; it renders the predicate the host composes into its own
/// query, and the host's context executes it.
/// </summary>
/// <param name="Name">The relationship, in the language of the host's domain.</param>
/// <param name="On">The resource type the fact is about.</param>
/// <param name="Relation">The relation the SQL rendering names.</param>
/// <param name="HolderColumn">The column of that relation naming the subject.</param>
/// <param name="ResourceColumn">The column of that relation naming the record.</param>
/// <param name="Holder">How a row names the subject holding the relationship.</param>
/// <param name="Resource">How a row names the record it is held on.</param>
/// <remarks>
/// Implements AUTHZ-DERIVE-001, AUTHZ-DERIVE-003, AUTHZ-DERIVE-004 and D-160. The two
/// selectors are the LINQ rendering's; the relation and the two column names are the
/// SQL rendering's.
/// </remarks>
public sealed record RelationshipDeclaration(
    string Name,
    ResourceType On,
    string Relation,
    string HolderColumn,
    string ResourceColumn,
    LambdaExpression Holder,
    LambdaExpression Resource)
{
    /// <summary>
    /// Every column the evaluation reads, each of which is indexed
    /// (AUTHZ-DERIVE-004).
    /// </summary>
    public IReadOnlyList<string> Columns => [HolderColumn, ResourceColumn];
}
