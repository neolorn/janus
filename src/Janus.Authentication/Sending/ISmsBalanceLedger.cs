using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Janus.Authentication.Sending;

/// <summary>
/// Where the gateway balance readings are kept, which is what makes a drain
/// measurable without anyone watching a dashboard.
/// </summary>
/// <remarks>Implements INT-SMS-004, AUTH-ABUSE-006 and CONV-DESIGN-003.</remarks>
internal interface ISmsBalanceLedger
{
    /// <summary>
    /// Keeps one reading and drops those too old to measure a drain against.
    /// </summary>
    /// <param name="reading">What was read.</param>
    /// <param name="retain">How far back readings are kept.</param>
    /// <param name="cancellationToken">Abandons the write.</param>
    /// <returns>The work of keeping it.</returns>
    ValueTask RecordAsync(BalanceReading reading, TimeSpan retain, CancellationToken cancellationToken);

    /// <summary>
    /// The readings taken since one instant, oldest first.
    /// </summary>
    /// <param name="from">The earliest reading wanted.</param>
    /// <param name="cancellationToken">Abandons the read.</param>
    /// <returns>The readings.</returns>
    ValueTask<IReadOnlyList<BalanceReading>> SinceAsync(
        DateTimeOffset from,
        CancellationToken cancellationToken);
}
