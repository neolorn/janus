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
/// used; where nothing can be asked, the absence is the record.
/// </remarks>
internal sealed class PhoneSignals(
    PhoneSignalProvider? provider,
    IPhoneSignalAudit audit,
    IUnitOfWork work,
    TimeProvider time)
{
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
