using System;
using System.Threading;
using System.Threading.Tasks;

namespace Janus.Authentication.Sending;

/// <summary>
/// What counts inbound callbacks per source, so that the endpoint answers a flood
/// before it looks anything up and a run of rejections is noticed.
/// </summary>
/// <remarks>Implements INT-GEN-003, BFF-MACH-003 and CONV-DESIGN-003.</remarks>
internal interface ICallbackLedger
{
    /// <summary>
    /// Counts one callback from a source and says how many that source has made in
    /// the window it falls in.
    /// </summary>
    /// <param name="source">Where it came from.</param>
    /// <param name="at">When.</param>
    /// <param name="window">The fixed window the count is taken over.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>How many that source has made, this one included.</returns>
    ValueTask<int> ReceivedAsync(
        string source,
        DateTimeOffset at,
        TimeSpan window,
        CancellationToken cancellationToken);

    /// <summary>
    /// Counts one rejected callback and says how many that source has had rejected
    /// since an instant.
    /// </summary>
    /// <param name="source">Where it came from.</param>
    /// <param name="at">When.</param>
    /// <param name="from">The earliest counted.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>How many were rejected, this one included.</returns>
    ValueTask<int> RejectedAsync(
        string source,
        DateTimeOffset at,
        DateTimeOffset from,
        CancellationToken cancellationToken);
}
