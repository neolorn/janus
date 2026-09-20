using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Recovery;
using Janus.Core;

namespace Janus.Authentication.Tests.Recovery;

/// <summary>
/// The loss reports that are running, held in memory. One stands per credential, as
/// the primary key holds it.
/// </summary>
internal sealed class LossReportStoreInMemory : ILossReportStore
{
    private readonly Dictionary<AuthenticatorId, LossReport> _running = [];

    /// <inheritdoc/>
    public ValueTask<LossReport?> FindAsync(
        AuthenticatorId credential,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult(_running.GetValueOrDefault(credential));

    /// <inheritdoc/>
    public ValueTask AddAsync(LossReport report, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(report);

        _running[report.Credential] = report;

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask RecordAsync(LossReport report, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(report);

        _running[report.Credential] = report;

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask RemoveAsync(AuthenticatorId credential, CancellationToken cancellationToken)
    {
        _ = _running.Remove(credential);

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask<IReadOnlyList<LossReport>> OutstandingAsync(
        DateTimeOffset notifiedBefore,
        DateTimeOffset invalidatingBefore,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult<IReadOnlyList<LossReport>>(
            [.. _running.Values.Where(report =>
                report.NotifiedAt <= notifiedBefore || report.InvalidatesAt <= invalidatingBefore)]);
}
