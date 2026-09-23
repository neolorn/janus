using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Sending;
using Microsoft.EntityFrameworkCore;

namespace Janus.Storage.Authentication.Sending;

/// <summary>
/// Where the gateway balance readings are kept, for as long as a drain is measured
/// over.
/// </summary>
/// <param name="context">The context the operation runs on.</param>
/// <remarks>Implements INT-SMS-004 and CONV-DESIGN-003.</remarks>
internal sealed class SmsBalanceLedger(StoreContext context) : ISmsBalanceLedger
{
    /// <inheritdoc/>
    public async ValueTask RecordAsync(
        BalanceReading reading,
        TimeSpan retain,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(reading);

        DateTimeOffset oldest = reading.At - retain;

        await context.SmsBalanceReadings
            .Where(kept => kept.ReadAt < oldest)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);

        BalanceReadingRecord? standing = await context.SmsBalanceReadings
            .FindAsync([reading.At], cancellationToken)
            .ConfigureAwait(false);

        if (standing is null)
        {
            context.SmsBalanceReadings.Add(new BalanceReadingRecord
            {
                ReadAt = reading.At,
                Balance = reading.Balance,
            });

            return;
        }

        standing.Balance = reading.Balance;
    }

    /// <inheritdoc/>
    public async ValueTask<IReadOnlyList<BalanceReading>> SinceAsync(
        DateTimeOffset from,
        CancellationToken cancellationToken) =>
        await context.SmsBalanceReadings
            .Where(reading => reading.ReadAt >= from)
            .OrderBy(reading => reading.ReadAt)
            .Select(reading => new BalanceReading(reading.ReadAt, reading.Balance))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
}
