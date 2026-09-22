using System;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Factors;
using Janus.Core;

namespace Janus.Authentication.Sending;

/// <summary>
/// What is known about a number before a factor the standard restricts is sent to it.
/// </summary>
/// <param name="provider">What the deployment registered to answer, where it did.</param>
/// <param name="audit">Where the outcome is written down.</param>
/// <param name="work">The one transaction an operation runs in.</param>
/// <param name="time">The clock the deployment runs on.</param>
/// <remarks>
/// Implements AUTH-FACT-002b. A text reaches a number and not a person, so what the
/// carrier reports about the number is considered before the entry that rides it is
/// used; where nothing can be asked, the absence is the record. A reported change of
/// SIM or of network withholds the entry from that sign-in, because the message would
/// reach whoever holds the number now.
/// </remarks>
internal sealed class PhoneSignals(
    PhoneSignalProvider? provider,
    IPhoneSignalAudit audit,
    IUnitOfWork work,
    TimeProvider time)
{
    /// <summary>
    /// Considers the number a sign-in is about to lean on, and answers whether the
    /// factor may still be used with it.
    /// </summary>
    /// <param name="factor">The entry the number would carry.</param>
    /// <param name="number">The number, canonically.</param>
    /// <param name="subject">Whose account it is.</param>
    /// <param name="cancellationToken">Abandons the consideration.</param>
    /// <returns>Whether the factor may be used.</returns>
    /// <remarks>
    /// A refusal is recorded here, because nothing is sent after it and the send is
    /// where a consideration is otherwise written down (AUTH-FACT-002b AC6). Any
    /// other answer leaves the record to the send that follows, so one use of a
    /// restricted entry writes one row.
    /// </remarks>
    public async ValueTask<bool> AllowsAsync(
        Factor factor,
        string number,
        SubjectId? subject,
        CancellationToken cancellationToken)
    {
        if (provider is null)
        {
            return true;
        }

        PhoneSignal answered = await provider.Signal(number, cancellationToken)
            .ConfigureAwait(false);

        if (answered is not PhoneSignal.Risk)
        {
            return true;
        }

        await work.BeginAsync(cancellationToken).ConfigureAwait(false);
        await audit
            .ConsideredAsync(factor, answered, subject, time.GetUtcNow(), cancellationToken)
            .ConfigureAwait(false);
        await work.CommitAsync(cancellationToken).ConfigureAwait(false);

        return false;
    }

    /// <summary>
    /// Considers the number one message is about to go to, where the message is a
    /// restricted factor, and records what was known.
    /// </summary>
    /// <param name="request">What is about to be sent.</param>
    /// <param name="cancellationToken">Abandons the consideration.</param>
    /// <returns>The work of considering it.</returns>
    /// <exception cref="ArgumentNullException">The request is absent.</exception>
    public async ValueTask ConsiderAsync(SendRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (Restricted(request) is not Factor factor)
        {
            return;
        }

        PhoneSignal? answered = provider is null
            ? null
            : await provider.Signal(request.Destination.Canonical, cancellationToken)
                .ConfigureAwait(false);

        await work.BeginAsync(cancellationToken).ConfigureAwait(false);
        await audit
            .ConsideredAsync(factor, answered, request.Subject, time.GetUtcNow(), cancellationToken)
            .ConfigureAwait(false);
        await work.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    // Which entry a message amounts to follows from the channel it goes out on and
    // whether it carries a link; whether that entry is restricted is the catalogue's.
    private static Factor? Restricted(SendRequest request) =>
        request.Kind is SendKind.Sms
        && MessageChannels.Factors.TryGetValue(request.Message, out bool link)
        && FactorCatalogue.Sent.TryGetValue((IdentifierKind.Phone, link), out Factor factor)
        && FactorCatalogue.Of(factor).Restricted
            ? factor
            : null;
}
