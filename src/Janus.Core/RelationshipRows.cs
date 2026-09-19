using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace Janus.Core;

/// <summary>
/// The rows of one declared relationship, as the host supplied them from its own
/// context, with the two motions a rendering asks of them.
/// </summary>
/// <remarks>
/// Implements AUTHZ-DERIVE-001, AUTHZ-DERIVE-005 and LIB-HOST-002 (D-160, D-161). The
/// host's row type is known where the host supplies the rows, so a rendering composes
/// over it without the library naming the type or reaching for it at runtime.
/// </remarks>
public sealed class RelationshipRows
{
    private readonly Func<LambdaExpression, Expression> _any;
    private readonly Func<LambdaExpression, LambdaExpression, IAsyncEnumerable<SubjectId>> _heldBy;

    internal RelationshipRows(
        Func<LambdaExpression, Expression> any,
        Func<LambdaExpression, LambdaExpression, IAsyncEnumerable<SubjectId>> heldBy)
    {
        _any = any;
        _heldBy = heldBy;
    }

    /// <summary>
    /// Whether the host holds any row satisfying the predicate, as an expression a
    /// rendering composes into the host's own query.
    /// </summary>
    /// <param name="predicate">A predicate over one row, typed by the host's own row.</param>
    /// <returns>The test.</returns>
    /// <exception cref="ArgumentNullException">The predicate is absent.</exception>
    public Expression Any(LambdaExpression predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);

        return _any(predicate);
    }

    /// <summary>
    /// The subjects holding the rows that satisfy the predicate, read from the host's
    /// own context as the rows arrive.
    /// </summary>
    /// <param name="predicate">A predicate over one row, typed by the host's own row.</param>
    /// <param name="holder">How a row names the subject holding it.</param>
    /// <returns>The subjects, each one once.</returns>
    /// <exception cref="ArgumentNullException">The predicate or the selector is absent.</exception>
    /// <exception cref="InvalidOperationException">
    /// The rows the host supplied are not read asynchronously.
    /// </exception>
    public IAsyncEnumerable<SubjectId> HeldBy(LambdaExpression predicate, LambdaExpression holder)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        ArgumentNullException.ThrowIfNull(holder);

        return _heldBy(predicate, holder);
    }
}
