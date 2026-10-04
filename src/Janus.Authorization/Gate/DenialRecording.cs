using System;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Authorization.Gate;

/// <summary>
/// The record of one refusal, written in a unit of work of its own: the row, the count
/// of its actor's refusals, and the spike that count raises.
/// </summary>
/// <param name="work">The transaction of the scope the record is written in.</param>
/// <param name="audit">Where the refusal is recorded and its actor's refusals are held.</param>
/// <param name="spikes">Where the refusal is counted against its actor.</param>
/// <remarks>
/// Implements AUTHZ-CONCEAL-004, AUTHZ-GATE-004, OPS-ALERT-001 and CONV-DESIGN-003
/// (D-183). The gate resolves this from a scope of its own, so the unit of work here is
/// never the caller's: the record commits at once and a rollback of the caller's work
/// leaves it standing, with the <c>denial-spike</c> alert and its <c>AlertRaised</c> row
/// where the refusal took its actor's count past the threshold. The actor's refusals are
/// held from before the row is written until the commit, so two refusals of one actor
/// at once are counted one after the other and the one that passes the threshold sees
/// that it does. A spike that cannot be written fails the record.
/// </remarks>
internal sealed class DenialRecording(IUnitOfWork work, IAccessAudit audit, DenialSpikes spikes)
{
    /// <summary>
    /// Records one refusal, counts it and raises what the count calls for, and commits.
    /// </summary>
    /// <param name="denial">What was refused, and to whom.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of recording it.</returns>
    /// <exception cref="ArgumentNullException">The refusal is absent.</exception>
    /// <exception cref="InvalidOperationException">
    /// The unit of work did not begin or commit, or the spike could not be raised: a
    /// fault naming the code of the failure.
    /// </exception>
    public async ValueTask RecordAsync(DeniedAccess denial, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(denial);

        (await work.BeginAsync(cancellationToken).ConfigureAwait(false))
            .Switch(_ => { }, error => throw Fault(error));

        await audit.HoldAsync(denial.Acting ?? default, denial.Principal, cancellationToken)
            .ConfigureAwait(false);
        await audit.RecordAsync(denial, cancellationToken).ConfigureAwait(false);

        if ((await spikes.WatchAsync(denial, cancellationToken).ConfigureAwait(false))
            .Match<Error?>(() => null, error => error) is Error unraised)
        {
            await work.RollbackAsync().ConfigureAwait(false);

            throw Fault(unraised);
        }

        (await work.CommitAsync(cancellationToken).ConfigureAwait(false))
            .Switch(() => { }, error => throw Fault(error));
    }

    // CONV-DESIGN-003: the record answers no result, so a failure of its unit of work or
    // of the raise is thrown as a fault naming the code the failure carries.
    private static InvalidOperationException Fault(Error error) => new(error.Code.ToString());
}
