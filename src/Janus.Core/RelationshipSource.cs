using System;
using System.Linq;

namespace Janus.Core;

/// <summary>
/// Where the library reads the rows of one declared relationship from the host's own
/// context, for the "who can access this?" view and the daily drift check of
/// materialised derivations. The host registers one per relationship a declared
/// derivation is over, materialised or not.
/// </summary>
/// <remarks>
/// Implements LIB-HOST-001, AUTHZ-DERIVE-005, AUTHZ-DERIVE-007 and LIB-HOST-002 (D-166,
/// D-183). The rows stay the host's: in the scope of the request or job run that reads,
/// the library takes one instance of the host's context from the container, the
/// contract tables that context maps and the rows this answers over that same instance,
/// so each evaluation is one query in the host's context. A relationship with no source,
/// or a source that does not hold together, stops the deployment at startup.
/// </remarks>
public sealed class RelationshipSource
{
    private readonly Action<object, IRelationshipRowsReader> _read;

    private RelationshipSource(
        string relationship,
        Type context,
        Type row,
        Action<object, IRelationshipRowsReader> read)
    {
        Relationship = relationship;
        Context = context;
        Row = row;
        _read = read;
    }

    /// <summary>
    /// The relationship the rows are of, as the model declares it.
    /// </summary>
    internal string Relationship { get; }

    /// <summary>
    /// The host's context the rows are read from, which the container gives in a scope.
    /// </summary>
    internal Type Context { get; }

    /// <summary>
    /// The row type the source answers, which is the one the derivation's selectors are
    /// declared over.
    /// </summary>
    internal Type Row { get; }

    /// <summary>
    /// The source of one relationship's rows.
    /// </summary>
    /// <typeparam name="TContext">
    /// The host's own context, which maps the contract tables and which the container
    /// gives in a scope.
    /// </typeparam>
    /// <typeparam name="TRow">The row type the derivation's selectors are declared over.</typeparam>
    /// <param name="relationship">The relationship, as the model declares it.</param>
    /// <param name="rows">The relationship's rows, answered from that context.</param>
    /// <returns>The source, for the host to register.</returns>
    /// <exception cref="ArgumentNullException">The rows are absent.</exception>
    /// <exception cref="ArgumentException">The relationship is absent or blank.</exception>
    public static RelationshipSource Of<TContext, TRow>(
        string relationship,
        Func<TContext, IQueryable<TRow>> rows)
        where TContext : class
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relationship);
        ArgumentNullException.ThrowIfNull(rows);

        return new RelationshipSource(
            relationship,
            typeof(TContext),
            typeof(TRow),
            (context, reader) => reader.Read(rows((TContext)context)));
    }

    /// <summary>
    /// Hands the rows, read from one instance of the host's context, to a reader that
    /// composes its query over them under their own type.
    /// </summary>
    /// <param name="context">The instance of the host's context the scope gave.</param>
    /// <param name="reader">What composes a query over the rows.</param>
    /// <exception cref="ArgumentNullException">The context or the reader is absent.</exception>
    internal void Read(object context, IRelationshipRowsReader reader)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(reader);

        _read(context, reader);
    }
}
