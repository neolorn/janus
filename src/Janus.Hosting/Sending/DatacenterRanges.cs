using System;
using System.Collections.Generic;
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
/// <param name="configuration">Where the file's maximum age and the signals counted are read.</param>
/// <param name="alerts">Where a degradation is raised.</param>
/// <param name="time">The clock the file's age is judged by.</param>
/// <remarks>
/// Implements AUTH-ABUSE-008 and OPS-OBS-002. An address is matched against the copy
/// held and never leaves the process. With no file held, or a file older than
/// <c>abuse.botdefence.ranges.maxage</c> by its own date, the answer is that the
/// address is in no range, so the signal does not fire, and the degradation is raised
/// under the scope chapter 10 section 5.23 gives each, so that the router carries one
/// alert a window rather than one a registration (OPS-ALERT-002). The bot defence asks
/// only while <c>datacenterRange</c> is among <c>abuse.botdefence.signals</c>, and each
/// run of the job raises the absence or the staleness only while it is, so nothing of
/// either is raised for a deployment that took the signal out of the set, and one that
/// counts it hears of them though no registration arrives. A read that fails is raised
/// where it fails, as the location file's is (INT-GEN-006).
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
    private const string Refused = "botdefence.ranges.refresh";

    /// <inheritdoc/>
    public async ValueTask<Result<bool>> ContainsAsync(string ipAddress, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ipAddress);

        // A process reads the file the first time it is asked, so a restart does not
        // wait for the next refresh; after that only the refresh reads it.
        if (!copy.Read
            && (await RefreshedAsync(cancellationToken).ConfigureAwait(false))
                .Match(() => (Error?)null, error => error) is Error unread)
        {
            return Result.Failure<bool>(unread);
        }

        if (await DegradationAsync(cancellationToken).ConfigureAwait(false) is string scope)
        {
            return (await RaisedAsync(scope, cancellationToken).ConfigureAwait(false))
                .Match(() => Result.Success(false), Result.Failure<bool>);
        }

        return Result.Success(
            copy.Held is DatacenterRangeFile file
            && IPAddress.TryParse(ipAddress, out IPAddress? address)
            && file.Contains(address));
    }

    /// <summary>
    /// Runs the job once: reads the file again and holds it in place of the copy held
    /// before, and then, while <c>datacenterRange</c> is among the signals counted,
    /// raises the absence or the staleness of what is held.
    /// </summary>
    /// <param name="context">The system principal the watch runs as.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// Nothing, or the failure where a degradation could not be raised. A refresh that
    /// failed keeps the copy held before it, until that copy is stale.
    /// </returns>
    /// <exception cref="ArgumentException">The context is not a principal that may monitor.</exception>
    public async ValueTask<Result> RefreshAsync(AccessContext context, CancellationToken cancellationToken)
    {
        _ = Monitoring(context);

        if ((await RefreshedAsync(cancellationToken).ConfigureAwait(false))
            .Match(() => (Error?)null, error => error) is Error unraised)
        {
            return Result.Failure(unraised);
        }

        IReadOnlySet<BotDefenceSignal> counted = (await configuration
                .ReadAsync(Settings.AbuseBotDefenceSignals, cancellationToken)
                .ConfigureAwait(false))
            .Match(value => value, error => throw new InvalidOperationException(error.Code.ToString()));

        // AUTH-ABUSE-008 AC6: a deployment that receives no registration hears of a
        // file that is absent or stale from the run itself.
        return counted.Contains(BotDefenceSignal.DatacenterRange)
            && await DegradationAsync(cancellationToken).ConfigureAwait(false) is string scope
            ? await RaisedAsync(scope, cancellationToken).ConfigureAwait(false)
            : Result.Success();
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
    // A read that fails is raised here and leaves the copy held before it in place;
    // what is left held is judged apart.
    private async ValueTask<Result> RefreshedAsync(CancellationToken cancellationToken)
    {
        copy.Tried();

        // With no file supplied there is nothing to refresh; the absence is raised
        // where what is held is judged.
        if (source is null)
        {
            return Result.Success();
        }

        Result<Stream> opened = await source.OpenAsync(cancellationToken).ConfigureAwait(false);

        if (opened.Match(stream => (Stream?)stream, _ => null) is not Stream stream)
        {
            return await RaisedAsync(Refused, cancellationToken).ConfigureAwait(false);
        }

        DatacenterRangeFile? read;

        await using (stream.ConfigureAwait(false))
        {
            read = await DatacenterRangeFile.ReadAsync(stream, cancellationToken).ConfigureAwait(false);
        }

        if (read is null)
        {
            return await RaisedAsync(Refused, cancellationToken).ConfigureAwait(false);
        }

        copy.Hold(read);

        return Result.Success();
    }

    // The scope of what keeps the copy held from answering, or nothing where it
    // answers: nothing held is the absence, and a copy a failed read left in place
    // answers until it is older than its maximum age by its own date.
    private async ValueTask<string?> DegradationAsync(CancellationToken cancellationToken)
    {
        if (copy.Held is not DatacenterRangeFile file)
        {
            return Absent;
        }

        TimeSpan maximumAge = (await configuration
                .ReadAsync(Settings.AbuseBotDefenceRangesMaxAge, cancellationToken)
                .ConfigureAwait(false))
            .Match(value => value, error => throw new InvalidOperationException(error.Code.ToString()));

        return DateOnly.FromDateTime(time.GetUtcNow().UtcDateTime).DayNumber - file.Produced.DayNumber
            > maximumAge.TotalDays
            ? Stale
            : null;
    }

    private async ValueTask<Result> RaisedAsync(string scope, CancellationToken cancellationToken) =>
        await alerts
            .RaiseAsync(Alerts.Scoped(AlertCondition.Degradation, scope, time.GetUtcNow()), cancellationToken)
            .ConfigureAwait(false);
}
