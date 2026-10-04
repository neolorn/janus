using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Factors;
using Janus.Authentication.Identifiers;
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
/// reach whoever holds the number now. The provider is the host's callback and may call
/// across the network, so it is asked before an operation's unit of work begins and
/// never inside one (CONV-DESIGN-002, D-186): what it answered is kept here, for the
/// scope of the request, until the send that follows records it in its unit of work.
/// </remarks>
internal sealed class PhoneSignals(
    PhoneSignalProvider? provider,
    IPhoneSignalAudit audit,
    IUnitOfWork work,
    TimeProvider time)
{
    // What the provider answered for a number, asked before the unit of work began and
    // not yet recorded by the send it was asked for.
    private readonly Dictionary<string, PhoneSignal> _answered = new(StringComparer.Ordinal);

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
    /// Called before the operation's unit of work begins, since it asks the host. A
    /// refusal is recorded here, in a unit of work of its own, because nothing is sent
    /// after it and the send is where a consideration is otherwise written down
    /// (AUTH-FACT-002b AC6). Any other answer is kept for the send that follows, which
    /// records it, so one use of a restricted entry writes one row.
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
            _answered[number] = answered;

            return true;
        }

        _ = _answered.Remove(number);

        (await work.BeginAsync(cancellationToken).ConfigureAwait(false))
            .Switch(() => { }, error => throw new InvalidOperationException(error.Code.ToString()));
        await audit
            .ConsideredAsync(factor, answered, subject, time.GetUtcNow(), cancellationToken)
            .ConfigureAwait(false);
        (await work.CommitAsync(cancellationToken).ConfigureAwait(false))
            .Switch(() => { }, error => throw new InvalidOperationException(error.Code.ToString()));

        return false;
    }

    /// <summary>
    /// Considers the number the account would be texted at, and answers whether the
    /// factor may still be used with it; an account holding no number is refused
    /// nothing here, since nothing can be texted to it.
    /// </summary>
    /// <param name="factor">The entry the number would carry.</param>
    /// <param name="held">The account's identifiers.</param>
    /// <param name="subject">Whose account it is.</param>
    /// <param name="cancellationToken">Abandons the consideration.</param>
    /// <returns>Whether the factor may be used.</returns>
    /// <exception cref="ArgumentNullException">The identifiers are absent.</exception>
    public async ValueTask<bool> AllowsAsync(
        Factor factor,
        HeldIdentifiers held,
        SubjectId subject,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(held);

        return held.Texted() is not HeldIdentifier texted
            || await AllowsAsync(factor, texted.Canonical, subject, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Records what was known about the number one message is about to go to, where the
    /// message is a restricted factor, in the unit of work that undertakes it.
    /// </summary>
    /// <param name="request">What is about to be sent.</param>
    /// <param name="cancellationToken">Abandons the consideration.</param>
    /// <returns>The work of considering it.</returns>
    /// <exception cref="ArgumentNullException">The request is absent.</exception>
    /// <exception cref="InvalidOperationException">
    /// A provider is declared and nothing was asked about the number before the unit of
    /// work began: the provider is never asked inside one.
    /// </exception>
    public async ValueTask ConsiderAsync(OutboundMessage request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (Restricted(request) is not Factor factor)
        {
            return;
        }

        PhoneSignal? answered = null;

        if (provider is not null)
        {
            answered = _answered.Remove(request.Destination.Canonical, out PhoneSignal asked)
                ? asked
                : throw new InvalidOperationException(
                    "The signal for a number is asked before the unit of work that sends to it begins.");
        }

        (await work.BeginAsync(cancellationToken).ConfigureAwait(false))
            .Switch(() => { }, error => throw new InvalidOperationException(error.Code.ToString()));
        await audit
            .ConsideredAsync(factor, answered, request.Subject, time.GetUtcNow(), cancellationToken)
            .ConfigureAwait(false);
        (await work.CommitAsync(cancellationToken).ConfigureAwait(false))
            .Switch(() => { }, error => throw new InvalidOperationException(error.Code.ToString()));
    }

    // Which entry a message amounts to follows from the channel it goes out on and
    // whether it carries a link; whether that entry is restricted is the catalogue's.
    private static Factor? Restricted(OutboundMessage request) =>
        request.Kind is SendKind.Sms
        && MessageChannels.Factors.TryGetValue(request.Message, out bool link)
        && FactorCatalogue.Sent.TryGetValue((IdentifierKind.Phone, link), out Factor factor)
        && FactorCatalogue.Of(factor).Restricted
            ? factor
            : null;
}
