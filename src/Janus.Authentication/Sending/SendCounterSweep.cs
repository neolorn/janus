using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Core.Configuration;

namespace Janus.Authentication.Sending;

/// <summary>
/// The expiry sweep's pass over the send counters: each record whose newest time
/// decides nothing under the restrictions now declared goes, whether or not its key is
/// sent to again.
/// </summary>
/// <param name="configuration">Where the restrictions are read.</param>
/// <param name="ledger">Where the counters are kept.</param>
/// <param name="time">The clock the deployment runs on.</param>
/// <remarks>Implements PRIV-RET-005 AC2 and AUTH-ABUSE-004 AC6.</remarks>
internal sealed class SendCounterSweep(
    IConfigurationStore configuration,
    ISendLedger ledger,
    TimeProvider time)
{
    /// <summary>
    /// Deletes every counter that decides nothing.
    /// </summary>
    /// <param name="cancellationToken">Abandons the pass.</param>
    /// <returns>Success, or the failure to read the restrictions.</returns>
    public async ValueTask<Result> SweepAsync(CancellationToken cancellationToken)
    {
        Error? failure = null;

        IReadOnlyList<Restriction> declared = (await configuration
                .ReadAsync(Settings.Restrictions, cancellationToken)
                .ConfigureAwait(false))
            .Match(value => value, error => Withheld<IReadOnlyList<Restriction>>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure(failure);
        }

        await ledger
            .SweepAsync(CounterStaleness.Of(declared, time.GetUtcNow()), cancellationToken)
            .ConfigureAwait(false);

        return Result.Success();
    }

    private static TValue Withheld<TValue>(Error error, ref Error? failure)
    {
        failure = error;

        return default!;
    }
}
