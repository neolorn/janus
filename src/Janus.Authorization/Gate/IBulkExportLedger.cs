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
}
