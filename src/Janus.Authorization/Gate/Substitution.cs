using System.Linq.Expressions;

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
    /// <inheritdoc/>
    protected override Expression VisitParameter(ParameterExpression node) =>
        node == parameter ? replacement : base.VisitParameter(node);
}
