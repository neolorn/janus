using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace Janus.Core;

/// <summary>
/// What the host supplies from its own context so that a permission filter composes
/// into its own query: the two contract tables, the rows of every relationship a
/// derivation follows from, and how a row of its table names the record the library
/// registered.
/// </summary>
/// <typeparam name="TResource">The host's row.</typeparam>
/// <remarks>
/// Implements AUTHZ-GATE-002, AUTHZ-DERIVE-001 and LIB-HOST-002. The library never
/// queries a host table: the queryables come from the host's context, the predicate is
/// applied to the host's query, and the result is one query.
/// </remarks>
public sealed class FilterSources<TResource>
{
    private readonly Dictionary<string, RelationshipRows> _relationships = new(StringComparer.Ordinal);

    /// <summary>
    /// The sources of one filter.
    /// </summary>
    /// <param name="ancestry">The ancestry closure from the host's context.</param>
    /// <param name="grants">The effective grants from the host's context.</param>
    /// <param name="identifier">How a row names the record it was registered as.</param>
    /// <exception cref="ArgumentNullException">One of them is absent.</exception>
    public FilterSources(
        IQueryable<AncestryEntry> ancestry,
        IQueryable<EffectiveGrant> grants,
        Expression<Func<TResource, string>> identifier)
    {
        ArgumentNullException.ThrowIfNull(ancestry);
        ArgumentNullException.ThrowIfNull(grants);
        ArgumentNullException.ThrowIfNull(identifier);

        Ancestry = ancestry;
        Grants = grants;
        Identifier = identifier;
    }

    /// <summary>
    /// The ancestry closure as the host's context maps it.
    /// </summary>
    public IQueryable<AncestryEntry> Ancestry { get; }

    /// <summary>
    /// The grants and the permissions their roles allow, as the host's context maps them.
    /// </summary>
    public IQueryable<EffectiveGrant> Grants { get; }

    /// <summary>
    /// How a row names the record it was registered as, which is the host's own text.
    /// </summary>
    public Expression<Func<TResource, string>> Identifier { get; }

    /// <summary>
    /// The rows of each declared relationship, by the name the model declares it
    /// under, each composing a predicate over one of its rows into the host's query.
    /// </summary>
    public IReadOnlyDictionary<string, RelationshipRows> Relationships => _relationships;

    /// <summary>
    /// Adds the rows of one declared relationship, from the host's own context, so that
    /// a derivation following from it composes into the same query.
    /// </summary>
    /// <typeparam name="TRelationship">The host's relationship row.</typeparam>
    /// <param name="name">The relationship, as the model declares it.</param>
    /// <param name="rows">The rows, as the host's context reads them.</param>
    /// <returns>These sources, for chaining.</returns>
    /// <exception cref="ArgumentNullException">The rows are absent.</exception>
    /// <exception cref="ArgumentException">
    /// The name is absent or blank, or the relationship is supplied twice.
    /// </exception>
    public FilterSources<TResource> Relationship<TRelationship>(
        string name,
        IQueryable<TRelationship> rows)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(rows);

        // The rows enter the tree the way the host's own query carries them, as the
        // value behind a reference rather than as a literal of the provider's type.
        Expression<Func<IQueryable<TRelationship>>> source = () => rows;

        RelationshipRows any = predicate => Expression.Call(
            typeof(Queryable),
            nameof(Queryable.Any),
            [typeof(TRelationship)],
            source.Body,
            Expression.Quote(predicate));

        if (!_relationships.TryAdd(name, any))
        {
            throw new ArgumentException("The relationship " + name + " is supplied twice.", nameof(name));
        }

        return this;
    }
}
