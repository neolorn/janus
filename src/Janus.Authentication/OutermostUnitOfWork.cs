using System;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Authentication;

/// <summary>
/// The unit of work an operation whose refusal keeps a write begins: the outermost one,
/// so that what the refusal keeps is committed whatever the caller's outcome.
/// </summary>
/// <remarks>
/// Implements CONV-DESIGN-003 and AUTHZ-INHERIT-002 (D-203, D-206). Such an operation is
/// never called inside another's unit of work, a host's own joined with
/// <c>UseUnitOfWorkAsync</c> included: a level that joins another's would leave the
/// count or record the refusal keeps to the outer level's commit, and a rollback there
/// would discard it. A call the operation makes to write a kept write (a throttle's
/// count, an audit record) joins the level this begins and is no such operation.
/// </remarks>
internal static class OutermostUnitOfWork
{
    /// <summary>
    /// Begins the outermost unit of work of an operation whose refusal keeps a write.
    /// </summary>
    /// <param name="work">The unit of work the operation runs in.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// Nothing once the outermost level is open, or the failure
    /// <see cref="IUnitOfWork.BeginAsync"/> answered where none could be opened.
    /// </returns>
    /// <exception cref="ArgumentNullException">The unit of work is absent.</exception>
    /// <exception cref="InvalidOperationException">
    /// The operation was called inside another's unit of work, a fault of the calling
    /// code (CONV-ERR-001). The level it joined is ended with a rollback, which marks
    /// the whole, so nothing of the outer unit of work commits.
    /// </exception>
    public static async ValueTask<Result> BeginOutermostAsync(
        this IUnitOfWork work,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(work);

        Result<bool> begun = await work.BeginAsync(cancellationToken).ConfigureAwait(false);

        if (begun.Match<Error?>(_ => null, error => error) is Error notBegun)
        {
            return Result.Failure(notBegun);
        }

        if (begun.Match(outermost => outermost, _ => false))
        {
            return Result.Success();
        }

        await work.RollbackAsync().ConfigureAwait(false);

        throw new InvalidOperationException(
            "An operation whose refusal keeps a write begins the outermost unit of work and is never called inside another's.");
    }
}
