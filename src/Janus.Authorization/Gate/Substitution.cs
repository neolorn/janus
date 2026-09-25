using System;
using System.Linq.Expressions;
using Janus.Core;

namespace Janus.Authorization.Gate;

/// <summary>
/// One parameter of an expression read as something else.
/// </summary>
/// <param name="parameter">The parameter to replace.</param>
/// <param name="replacement">What to read it as.</param>
/// <remarks>
/// Implements AUTHZ-PRIN-001 and AUTHZ-DERIVE-001. A predicate is written once over an
/// identifier or a row and then read over the host's own, so that one rule serves every
/// rendering rather than several being kept alike by hand.
/// </remarks>
internal sealed class Substitution(ParameterExpression parameter, Expression replacement)
    : ExpressionVisitor
{
    /// <summary>
    /// The predicate over one of a relationship's rows that holds where the row is
    /// held on the record, typed by the host's own row.
    /// </summary>
    /// <param name="relationship">The relationship the rows are.</param>
    /// <param name="resource">The record the rows are to be held on.</param>
    /// <returns>The predicate.</returns>
    public static LambdaExpression HeldOn(RelationshipDeclaration relationship, ResourceId resource)
    {
        ArgumentNullException.ThrowIfNull(relationship);

        string named = resource.ToString();
        Expression<Func<string, bool>> names = value => value == named;

        return Expression.Lambda(
            new Substitution(names.Parameters[0], relationship.Resource.Body).Visit(names.Body),
            relationship.Resource.Parameters[0]);
    }

    /// <inheritdoc/>
    protected override Expression VisitParameter(ParameterExpression node) =>
        node == parameter ? replacement : base.VisitParameter(node);
}
