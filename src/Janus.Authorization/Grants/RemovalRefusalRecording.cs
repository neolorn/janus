using System;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Authorization.Grants;

/// <summary>
/// The record of one removal of access the host's check refused, written in a unit of work
/// of its own.
/// </summary>
/// <param name="work">The transaction of the scope the record is written in.</param>
/// <param name="audit">Where the refusal is written down.</param>
/// <param name="time">The clock the deployment runs on.</param>
/// <remarks>
/// Implements AUTHZ-GRANT-005 (D-281). The guard resolves this from a scope of its own, so
/// the unit of work here is never the change's: the record commits at once and the
/// rollback of the change it refused leaves it standing.
/// </remarks>
internal sealed class RemovalRefusalRecording(IUnitOfWork work, IGrantAudit audit, TimeProvider time)
{
    /// <summary>
    /// Records one refusal and commits.
    /// </summary>
    /// <param name="removal">What was refused.</param>
    /// <param name="code">The code it was refused with.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of recording it.</returns>
    /// <exception cref="ArgumentNullException">The removal is absent.</exception>
    /// <exception cref="InvalidOperationException">
    /// The unit of work did not begin or commit: a fault naming the code of the failure.
    /// </exception>
    public async ValueTask RecordAsync(AccessRemoval removal, ErrorCode code, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(removal);

        (await work.BeginAsync(cancellationToken).ConfigureAwait(false))
            .Switch(_ => { }, error => throw Fault(error));

        await audit.RemovalRefusedAsync(removal, code, time.GetUtcNow(), cancellationToken).ConfigureAwait(false);

        (await work.CommitAsync(cancellationToken).ConfigureAwait(false))
            .Switch(() => { }, error => throw Fault(error));
    }

    // CONV-DESIGN-003: the record answers no result, so a failure of its unit of work is
    // thrown as a fault naming the code the failure carries.
    private static InvalidOperationException Fault(Error error) => new(error.Code.ToString());
}
