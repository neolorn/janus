using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Authorization.Gate;

/// <summary>
/// When each actor's recent export operations were admitted, which is what
/// <c>exfiltration.export.ratelimit</c> is counted against.
/// </summary>
/// <remarks>
/// Implements OPS-ALERT-006. An actor is a person, or a system principal by its name
/// where no person acts. What an export returned is the audit trail's and the host's,
/// never this ledger's.
/// </remarks>
internal interface IBulkExportLedger
{
    /// <summary>
    /// Holds one actor's exports against every other admission of the actor's until the
    /// operation's transaction ends, so the hour is counted on what is committed and
    /// two admissions at once are counted one after the other (CONV-DESIGN-003).
    /// </summary>
    /// <param name="actor">The person, where a person acts.</param>
    /// <param name="principal">The system principal's name, where one acts.</param>
    /// <param name="cancellationToken">Abandons the wait.</param>
    /// <returns>The work of holding them.</returns>
    /// <exception cref="InvalidOperationException">No transaction is open.</exception>
    ValueTask HoldAsync(SubjectId? actor, string? principal, CancellationToken cancellationToken);

    /// <summary>
    /// When one actor's exports since an instant were admitted, oldest first.
    /// </summary>
    /// <param name="actor">The person, where a person acts.</param>
    /// <param name="principal">The system principal's name, where one acts.</param>
    /// <param name="since">The start of the window.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The instants.</returns>
    ValueTask<IReadOnlyList<DateTimeOffset>> SinceAsync(
        SubjectId? actor,
        string? principal,
        DateTimeOffset since,
        CancellationToken cancellationToken);

    /// <summary>
    /// Records one admitted export, and forgets the actor's exports older than the
    /// window, which nothing counts again.
    /// </summary>
    /// <param name="actor">The person, where a person acts.</param>
    /// <param name="principal">The system principal's name, where one acts.</param>
    /// <param name="at">When it was admitted.</param>
    /// <param name="since">The start of the window, before which the actor's exports are forgotten.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of recording it.</returns>
    ValueTask RecordAsync(
        SubjectId? actor,
        string? principal,
        DateTimeOffset at,
        DateTimeOffset since,
        CancellationToken cancellationToken);

    /// <summary>
    /// Forgets every actor's exports admitted at or before an instant, which no limit
    /// counts again.
    /// </summary>
    /// <param name="since">The start of the window.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>How many were forgotten.</returns>
    /// <remarks>Implements OPS-OBS-003 and IDN-PRIN-003 AC4 (D-166, 329).</remarks>
    ValueTask<int> SweepAsync(DateTimeOffset since, CancellationToken cancellationToken);
}
