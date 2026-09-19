using System;
using System.Linq;
using System.Linq.Expressions;

namespace Janus.Core;

/// <summary>
/// What the host supplies from its own context so that a permission filter composes
/// into its own query: the two contract tables and how a row of its table names the
/// record the library registered.
/// </summary>
/// <typeparam name="TResource">The host's row.</typeparam>
/// <remarks>
/// Implements AUTHZ-GATE-002 and LIB-HOST-002. The library never queries a host table:
/// the queryables come from the host's context, the predicate is applied to the host's
/// query, and the result is one query.
/// </remarks>
public sealed class FilterSources<TResource>
{
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
}
