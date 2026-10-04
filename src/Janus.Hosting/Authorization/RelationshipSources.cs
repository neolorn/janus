using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authorization.Gate;
using Janus.Core;
using Microsoft.EntityFrameworkCore;

namespace Janus.Hosting.Authorization;

/// <summary>
/// The rows of the declared relationships, read through the sources the host declared.
/// </summary>
/// <param name="declared">The relationship sources the host registered.</param>
/// <param name="scope">The scope of the request or job run that reads.</param>
/// <remarks>
/// Implements LIB-HOST-001, LIB-HOST-002, AUTHZ-DERIVE-005 and AUTHZ-DERIVE-007 (D-166,
/// D-183). In the scope that reads, one instance of the host's context is taken from the
/// container, with the ancestry closure it maps and the rows the source answers over
/// that same instance, so a reading is one query in the host's context and the library
/// issues none of its own against a host table. The startup check holds every source to
/// its declaration, so a source that does not fit is a fault here and not a refusal.
/// </remarks>
internal sealed class RelationshipSources(IEnumerable<RelationshipSource> declared, IServiceProvider scope)
    : IRelationshipSources
{
    /// <inheritdoc/>
    public bool Declares(string relationship) =>
        declared.Any(source => string.Equals(source.Relationship, relationship, StringComparison.Ordinal));

    /// <inheritdoc/>
    public IAsyncEnumerable<HeldRelationship> HeldAbove(
        RelationshipDeclaration relationship,
        ResourceReference resource)
    {
        ArgumentNullException.ThrowIfNull(relationship);

        (RelationshipSource source, DbContext context) = Opened(relationship);
        var reader = new AboveReader(relationship, resource, context.Set<AncestryEntry>());

        source.Read(context, reader);

        return HeldAsync(reader.Held);
    }

    private static async IAsyncEnumerable<HeldRelationship> HeldAsync(
        IQueryable<Above> held,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (Above each in held.AsAsyncEnumerable().WithCancellation(cancellationToken))
        {
            yield return new HeldRelationship(each.Holder, ResourceId.Parse(each.Resource));
        }
    }

    // The rows as the pair every reading composes over: who holds each and the record it
    // names, through the two selectors the model declares for the relationship. The
    // selectors are typed by the host's own row, which the source hands over, so the
    // projection is built from them and nothing reads the row's type at runtime
    // (CONV-CODE-004).
    private static IQueryable<Row> Rows<TRow>(IQueryable<TRow> rows, RelationshipDeclaration relationship)
    {
        var holder = (Expression<Func<TRow, SubjectId>>)relationship.Holder;
        var resource = (Expression<Func<TRow, string>>)relationship.Resource;

        Expression<Func<SubjectId, string, Row>> shape = (held, named) => new Row { Holder = held, Resource = named };

        ParameterExpression row = holder.Parameters[0];
        Expression named = new Substitution(resource.Parameters[0], row).Visit(resource.Body);
        Expression body = new Substitution(shape.Parameters[0], holder.Body).Visit(shape.Body);

        return rows.Select(Expression.Lambda<Func<TRow, Row>>(
            new Substitution(shape.Parameters[1], named).Visit(body),
            row));
    }

    // LIB-HOST-001: the source the host declared for the relationship and the one
    // instance of its context the reading scope gives.
    private (RelationshipSource Source, DbContext Context) Opened(RelationshipDeclaration relationship)
    {
        RelationshipSource source = declared.FirstOrDefault(each =>
                string.Equals(each.Relationship, relationship.Name, StringComparison.Ordinal))
            ?? throw new InvalidOperationException(string.Create(
                CultureInfo.InvariantCulture,
                $"No source is declared for the relationship '{relationship.Name}'."));

        return scope.GetService(source.Context) is DbContext context
            ? (source, context)
            : throw new InvalidOperationException(string.Create(
                CultureInfo.InvariantCulture,
                $"The container gives no context for the source of the relationship '{relationship.Name}'."));
    }

    // AUTHZ-DERIVE-007: who holds the relationship on the record or on a container of
    // the relationship's type, nearest first, as one statement over the rows and the
    // ancestry of one context instance.
    private sealed class AboveReader(
        RelationshipDeclaration relationship,
        ResourceReference resource,
        IQueryable<AncestryEntry> ancestry) : IRelationshipRowsReader
    {
        private IQueryable<Above>? _held;

        public IQueryable<Above> Held =>
            _held ?? throw new InvalidOperationException("The source handed over no rows.");

        public void Read<TRow>(IQueryable<TRow> rows)
        {
            string type = resource.Type.ToString();
            string id = resource.Id.ToString();
            string on = relationship.On.ToString();

            _held = Rows(rows, relationship)
                .Join(
                    ancestry.Where(entry =>
                        entry.ResourceType == type
                        && entry.ResourceId == id
                        && entry.AncestorType == on),
                    row => row.Resource,
                    entry => entry.AncestorId,
                    (row, entry) => new Above { Holder = row.Holder, Resource = row.Resource, Depth = entry.Depth })
                .Distinct()
                .OrderBy(above => above.Depth);
        }
    }

    // One row of a relationship as every reading composes over it.
    private sealed class Row
    {
        public SubjectId Holder { get; init; }

        public string Resource { get; init; } = string.Empty;
    }

    // One holder on the record or a container of it, with how far above the container
    // sits.
    private sealed class Above
    {
        public SubjectId Holder { get; init; }

        public string Resource { get; init; } = string.Empty;

        public int Depth { get; init; }
    }
}
