using System.Collections.Generic;

namespace Janus.Core;

/// <summary>
/// One fact in the host's own data that a derivation may follow from: a column of one
/// of the host's tables naming a subject. The library never reads the table; it
/// renders the predicate the host composes into its own query.
/// </summary>
/// <param name="Name">The relationship, in the language of the host's domain.</param>
/// <param name="On">The resource type the fact is about.</param>
/// <param name="Table">The host table holding it.</param>
/// <param name="SubjectColumn">The column of that table naming the subject.</param>
/// <param name="Columns">Every column the evaluation reads, each of which is indexed.</param>
/// <remarks>Implements AUTHZ-DERIVE-001, AUTHZ-DERIVE-003 and AUTHZ-DERIVE-004.</remarks>
public sealed record RelationshipDeclaration(
    string Name,
    ResourceType On,
    string Table,
    string SubjectColumn,
    IReadOnlyList<string> Columns);
