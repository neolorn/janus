using System;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Alerting;
using Janus.Authentication.Sending;
using Janus.Core;
using Janus.Core.Configuration;

namespace Janus.Hosting.Sending;

/// <summary>
/// The datacenter ranges of the bot defence: the file the deployment supplies, read in
/// process and matched against in memory.
/// </summary>
/// <param name="copy">The copy this process holds.</param>
/// <param name="source">Where the file is opened, or nothing where the deployment supplies none.</param>
/// <param name="configuration">Where the file's maximum age is read.</param>
/// <param name="alerts">Where a degradation is raised.</param>
/// <param name="time">The clock the file's age is judged by.</param>
/// <remarks>
/// Implements AUTH-ABUSE-008 and OPS-OBS-002. An address is matched against the copy
/// held and never leaves the process. With no file held, or a file older than
/// <c>abuse.botdefence.ranges.maxage</c> by its own date, the answer is that the
/// address is in no range, so the signal does not fire, and the degradation is raised
/// under the scope chapter 10 section 5.23 gives each, so that the router carries one
/// alert a window rather than one a registration (OPS-ALERT-002). The bot defence asks
/// only while <c>datacenterRange</c> is among <c>abuse.botdefence.signals</c>, so
/// nothing is raised for a deployment that took the signal out of the set.
/// </remarks>
internal sealed class DatacenterRanges(
    DatacenterRangeCopy copy,
    IDatacenterRangeSource? source,
    IConfigurationStore configuration,
    IAlertChannels alerts,
    TimeProvider time) : IDatacenterRanges
{
    private const string Absent = "botdefence.ranges.absent";
    private const string Stale = "botdefence.ranges.stale";

    /// <inheritdoc/>
    public async ValueTask<Result<bool>> ContainsAsync(string ipAddress, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ipAddress);

        // A process reads the file the first time it is asked, so a restart does not
        // wait for the next refresh; after that only the refresh reads it.
        if (!copy.Read)
        {
            await RefreshedAsync(cancellationToken).ConfigureAwait(false);
        }

        if (copy.Held is not DatacenterRangeFile file)
        {
            return await DegradedAsync(Absent, cancellationToken).ConfigureAwait(false);
        }

        TimeSpan maximumAge = (await configuration
                .ReadAsync(Settings.AbuseBotDefenceRangesMaxAge, cancellationToken)
                .ConfigureAwait(false))
            .Match(value => value, error => throw new InvalidOperationException(error.Code.ToString()));

        if (DateOnly.FromDateTime(time.GetUtcNow().UtcDateTime).DayNumber - file.Produced.DayNumber
            > maximumAge.TotalDays)
        {
            return await DegradedAsync(Stale, cancellationToken).ConfigureAwait(false);
        }

        return Result.Success(IPAddress.TryParse(ipAddress, out IPAddress? address) && file.Contains(address));
    }

    /// <summary>
    /// Reads the file again and holds it in place of the copy held before. A refresh
    /// that failed keeps the copy held before it, until that copy is stale.
    /// </summary>
    /// <param name="context">The system principal the watch runs as.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of reading it.</returns>
    /// <exception cref="ArgumentException">The context is not a principal that may monitor.</exception>
    public async ValueTask RefreshAsync(AccessContext context, CancellationToken cancellationToken)
    {
        _ = Monitoring(context);

        await RefreshedAsync(cancellationToken).ConfigureAwait(false);
    }

    // INF-BG-002 AC1, IDN-PRIN-001 AC3 (D-166, 304): the watch runs as a named
    // principal that may monitor, and never as nobody.
    private static SystemPrincipal Monitoring(AccessContext context) =>
        context?.Principal is { } principal && principal.MayRun(SystemOperation.Monitoring)
            ? principal
            : throw new ArgumentException(
                "The watch runs as a system principal that may monitor.",
                nameof(context));

    // The read itself, which the first address a process is asked about makes as well.
    // What it leaves held is judged where an address is asked about: nothing held is
    // the absence, and a copy a failed read left in place answers until it is stale.
    private async ValueTask RefreshedAsync(CancellationToken cancellationToken)
    {
        copy.Tried();

        if (source is null)
        {
            return;
        }

        Result<Stream> opened = await source.OpenAsync(cancellationToken).ConfigureAwait(false);

        if (opened.Match(stream => (Stream?)stream, _ => null) is not Stream stream)
        {
            return;
        }

        DatacenterRangeFile? read;

        await using (stream.ConfigureAwait(false))
        {
            read = await DatacenterRangeFile.ReadAsync(stream, cancellationToken).ConfigureAwait(false);
        }

        if (read is not null)
        {
            copy.Hold(read);
        }
    }

    private async ValueTask<Result<bool>> DegradedAsync(string scope, CancellationToken cancellationToken) =>
        (await alerts
            .RaiseAsync(Alerts.Scoped(AlertCondition.Degradation, scope, time.GetUtcNow()), cancellationToken)
            .ConfigureAwait(false))
        .Match(() => Result.Success(false), Result.Failure<bool>);
}
