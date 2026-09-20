using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Authentication.Recovery;

/// <summary>
/// Where the loss reports that are running are held. One stands per credential.
/// </summary>
/// <remarks>Implements AUTH-RECOV-007 and CONV-DESIGN-003.</remarks>
internal interface ILossReportStore
{
    /// <summary>
    /// The report running against one credential.
    /// </summary>
    /// <param name="credential">Which credential.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The report, or nothing where none is running.</returns>
    ValueTask<LossReport?> FindAsync(
        AuthenticatorId credential,
        CancellationToken cancellationToken);

    /// <summary>
    /// Opens a report.
    /// </summary>
    /// <param name="report">The report.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of opening it.</returns>
    ValueTask AddAsync(LossReport report, CancellationToken cancellationToken);

    /// <summary>
    /// Carries a change a report made onto its row.
    /// </summary>
    /// <param name="report">The report as it now stands.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of recording it.</returns>
    ValueTask RecordAsync(LossReport report, CancellationToken cancellationToken);

    /// <summary>
    /// Closes a report, which cancelling it and invalidating the credential both do.
    /// </summary>
    /// <param name="credential">Which credential.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of closing it.</returns>
    ValueTask RemoveAsync(AuthenticatorId credential, CancellationToken cancellationToken);

    /// <summary>
    /// The reports that owe something: a notice not sent since an instant, or a window
    /// that has ended.
    /// </summary>
    /// <param name="notifiedBefore">The latest notice that still owes another.</param>
    /// <param name="invalidatingBefore">The latest window end that has fallen due.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The reports.</returns>
    ValueTask<IReadOnlyList<LossReport>> OutstandingAsync(
        DateTimeOffset notifiedBefore,
        DateTimeOffset invalidatingBefore,
        CancellationToken cancellationToken);
}
