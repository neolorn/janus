using System;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Callbacks;
using Janus.Core;

namespace Janus.Authentication.Sending;

/// <summary>
/// The delivery-report callback, treated as what it is: an unauthenticated request
/// over plain HTTP with its parameters in the query string. The one state it may
/// change is to take a send back out of the buckets it counted against.
/// </summary>
/// <param name="admission">What counts callbacks per source and raises the alert.</param>
/// <param name="ledger">Where the send was counted.</param>
/// <param name="work">The one transaction an operation runs in.</param>
/// <remarks>
/// Implements AUTH-ABUSE-007, INT-SMS-005 and INT-GEN-003. A forged failure report
/// gains an attacker at most one extra send to a number the restriction already
/// allows; nothing here can mark a phone verified.
/// </remarks>
internal sealed class DeliveryReports(
    CallbackAdmission admission,
    ISendLedger ledger,
    IUnitOfWork work)
{
    /// <summary>
    /// Takes one delivery report.
    /// </summary>
    /// <param name="source">Where the callback came from.</param>
    /// <param name="reference">The correlation reference it carried.</param>
    /// <param name="delivered">What it says became of the message.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// Nothing where a failed send was released or a delivered one is held, or
    /// <c>integration.callback.rejected</c> where the callback was rate-limited or
    /// carried a reference no live send answers to.
    /// </returns>
    /// <exception cref="ArgumentNullException">The source is absent.</exception>
    public async ValueTask<Result> ReportAsync(
        string source,
        string? reference,
        bool delivered,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);

        await work.BeginAsync(cancellationToken).ConfigureAwait(false);

        // Answered before any lookup, so a flood costs the deployment nothing beyond
        // the count it was already keeping (INT-GEN-003).
        Result admitted = await admission.AdmitAsync(source, cancellationToken).ConfigureAwait(false);

        if (admitted.Match(() => false, _ => true))
        {
            return await KeptAsync(admitted, cancellationToken).ConfigureAwait(false);
        }

        // A report that a message arrived advances nothing at all: the state it might
        // seem to confirm is proved by the code the person types, never by the
        // gateway saying so (AUTH-ABUSE-007). It is still held to a live send, so a
        // guessed reference is refused whatever it reports (INT-GEN-003 AC1).
        bool answered = !string.IsNullOrWhiteSpace(reference)
            && await AnsweredAsync(SendReferences.Of(reference), delivered, cancellationToken)
                .ConfigureAwait(false);

        Result outcome = answered
            ? Result.Success()
            : await admission.RejectAsync(source, cancellationToken).ConfigureAwait(false);

        return await KeptAsync(outcome, cancellationToken).ConfigureAwait(false);
    }

    // A report of delivery is checked against the send and changes nothing of it; a
    // report of failure releases it.
    private ValueTask<bool> AnsweredAsync(byte[] reference, bool delivered, CancellationToken cancellationToken) =>
        delivered
            ? ledger.HoldsAsync(reference, cancellationToken)
            : ledger.ReleaseAsync(reference, cancellationToken);

    // The counts are kept whenever the callback was answered, a rejection included;
    // a failure of anything else leaves the transaction to roll back.
    private async ValueTask<Result> KeptAsync(Result outcome, CancellationToken cancellationToken)
    {
        if (outcome.Match(() => true, error => error.Code == ErrorCodes.CallbackRejected))
        {
            await work.CommitAsync(cancellationToken).ConfigureAwait(false);
        }

        return outcome;
    }
}
