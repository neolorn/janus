using System.Linq.Expressions;

namespace Janus.Core;

/// <summary>
/// Composes a predicate over one row of a relationship into a test of whether the host
/// holds any row satisfying it, over the rows the host supplied from its own context.
/// </summary>
/// <param name="predicate">A predicate over one row, typed by the host's own row.</param>
/// <returns>The test, for the rendering to compose into the host's query.</returns>
/// <remarks>
/// Implements AUTHZ-DERIVE-001 and LIB-HOST-002 (D-160). The host's row type is known
/// where the host supplies the rows, so the rendering composes a predicate over it
/// without the library naming the type or reaching for it at runtime.
/// </remarks>
public delegate Expression RelationshipRows(LambdaExpression predicate);
