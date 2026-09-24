using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Privacy.Erasures;

/// <summary>
/// The library's own required confirmation on every erasure's delivery: the erasure's
/// line appended to the off-host ledger.
/// </summary>
/// <param name="ledger">Where the line is appended.</param>
/// <remarks>
/// Implements DR-016 AC2 and IDN-LIFE-003a. The line rides the delivery as a required
/// subscriber's confirmation does, so an erasure whose line is not durable stays
/// outstanding, is retried on the delivery's schedule, is raised when the budget is
/// spent and is listed with the rest of its delivery's progress. It is offered first,
/// so the line is written as early as the erasure can be reported.
/// </remarks>
internal sealed class ErasureLedgerSubscriber(IErasureLedger ledger) : ISubjectEventSubscriber
{
    /// <summary>
    /// What the confirmation is recorded under. No host subscriber may take the name.
    /// </summary>
    public const string Called = "erasure-ledger";

    /// <inheritdoc/>
    public string Name => Called;

    /// <inheritdoc/>
    public bool Required => true;

    /// <inheritdoc/>
    public IReadOnlyCollection<ResourceType> Covers { get; } = [];

    /// <summary>
    /// The subscribers an erasure's delivery waits for: the ledger's, where the
    /// deployment registered a ledger, then the host's own.
    /// </summary>
    /// <param name="subscribers">The subscribers the host registered.</param>
    /// <param name="ledger">The ledger the deployment registered, if any.</param>
    /// <returns>The subscribers, the ledger's first.</returns>
    /// <exception cref="ArgumentNullException">The subscribers are absent.</exception>
    public static IReadOnlyList<ISubjectEventSubscriber> Joined(
        IEnumerable<ISubjectEventSubscriber> subscribers,
        IErasureLedger? ledger)
    {
        ArgumentNullException.ThrowIfNull(subscribers);

        return ledger is null
            ? [.. subscribers]
            : [new ErasureLedgerSubscriber(ledger), .. subscribers];
    }

    /// <inheritdoc/>
    public async ValueTask<Result> HandleAsync(SubjectEvent raised, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(raised);

        if (raised is not ErasureRequested erased)
        {
            return Result.Success();
        }

        return await ledger
            .AppendAsync(ErasureLedgerLine.Of(erased).Written(), cancellationToken)
            .ConfigureAwait(false);
    }
}
