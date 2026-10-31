using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Authentication.Sending;

/// <summary>
/// The host's report of what became of a message the library handed it: a failure takes
/// the message back out of the buckets it counted against, and a delivered
/// <c>secondfactor</c> SMS takes its send out of those of the restrictions marked
/// <c>resend</c>.
/// </summary>
/// <param name="ledger">Where the send was counted.</param>
/// <param name="work">The one transaction a report runs in.</param>
/// <remarks>
/// Implements INT-SMS-005 and AUTH-ABUSE-004 (D-197, D-225). A report
/// marks nothing verified: the state it might seem to confirm is proved by the code the
/// person types. A false failure report gains at most one message to an address the
/// restrictions already allowed, and a false delivery report at most one text sooner
/// than a restriction marked <c>resend</c> would allow.
/// </remarks>
internal sealed class SendOutcomes(ISendLedger ledger, IUnitOfWork work) : ISendOutcomes
{
    /// <inheritdoc/>
    public async ValueTask<Result<bool>> ReportAsync(
        SendReference reference,
        bool delivered,
        CancellationToken cancellationToken)
    {
        if ((await work.BeginOutermostAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(() => null, error => error) is Error notBegun)
        {
            return Result.Failure<bool>(notBegun);
        }

        bool named;

        try
        {
            byte[] hashed = SendReferences.Of(reference);

            named = delivered
                ? await ledger.DeliveredAsync(hashed, cancellationToken).ConfigureAwait(false)
                : await ledger.ReleaseAsync(hashed, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await work.RollbackAsync().ConfigureAwait(false);

            throw;
        }

        return (await work.CommitAsync(cancellationToken).ConfigureAwait(false))
            .Match(() => Result.Success(named), Result.Failure<bool>);
    }
}
