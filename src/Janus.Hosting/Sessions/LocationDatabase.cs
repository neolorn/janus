using System;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Alerting;
using Janus.Authentication.Sessions;
using Janus.Core;
using Janus.Core.Configuration;

namespace Janus.Hosting.Sessions;

/// <summary>
/// The local IP-to-city database: the file the deployment supplies, read in process
/// and resolved against in memory.
/// </summary>
/// <param name="copy">The copy this process holds.</param>
/// <param name="source">Where the file is opened, or nothing where the deployment supplies none.</param>
/// <param name="configuration">Where the file's maximum age is read.</param>
/// <param name="alerts">Where a degradation is raised.</param>
/// <param name="time">The clock the file's age is judged by.</param>
/// <remarks>
/// Implements INT-GEN-006 and AUTH-SESS-013. An address is resolved against the copy
/// held and never leaves the process. With no file, a file refused or a file older than
/// <c>location.database.maxage</c>, no location is answered and the degradation is
/// raised, each under a scope of its own so that the router carries one alert a window
/// rather than one a sign-in (OPS-ALERT-002); the session is recorded without a
/// location either way.
/// </remarks>
internal sealed class LocationDatabase(
    LocationCopy copy,
    ILocationSource? source,
    IConfigurationStore configuration,
    IAlertChannels alerts,
    TimeProvider time) : ILocationResolver
{
    private const string Absent = "location.database.absent";
    private const string Stale = "location.database.stale";
    private const string Refused = "location.database.refresh";

    /// <inheritdoc/>
    public async ValueTask<Result<ResolvedLocation?>> ResolveAsync(
        string ipAddress,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ipAddress);

        // A process reads the file the first time it is asked, so a restart does not
        // wait for the next refresh; after that only the refresh reads it.
        if (!copy.Read
            && (await RefreshAsync(cancellationToken).ConfigureAwait(false))
                .Match(() => (Error?)null, error => error) is Error unread)
        {
            return Result.Failure<ResolvedLocation?>(unread);
        }

        if (copy.Held is not LocationFile file)
        {
            return await DegradedAsync(Absent, cancellationToken).ConfigureAwait(false);
        }

        TimeSpan maximumAge = (await configuration
                .ReadAsync(Settings.LocationDatabaseMaxAge, cancellationToken)
                .ConfigureAwait(false))
            .Match(value => value, _ => Settings.LocationDatabaseMaxAge.Default);

        if (DateOnly.FromDateTime(time.GetUtcNow().UtcDateTime).DayNumber - file.Produced.DayNumber
            > maximumAge.TotalDays)
        {
            return await DegradedAsync(Stale, cancellationToken).ConfigureAwait(false);
        }

        return Result.Success(IPAddress.TryParse(ipAddress, out IPAddress? address) ? file.Find(address) : null);
    }

    /// <summary>
    /// Reads the file again and holds it in place of the copy held before.
    /// </summary>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// Nothing, or the failure where a refresh that failed could not be raised. A
    /// refresh that failed keeps the copy held before it, until that copy is stale.
    /// </returns>
    public async ValueTask<Result> RefreshAsync(CancellationToken cancellationToken)
    {
        copy.Tried();

        // With no file supplied there is nothing to refresh; the absence is raised
        // where an address goes unresolved.
        if (source is null)
        {
            return Result.Success();
        }

        Result<Stream> opened = await source.OpenAsync(cancellationToken).ConfigureAwait(false);

        if (opened.Match(stream => (Stream?)stream, _ => null) is not Stream stream)
        {
            return await RaisedAsync(Refused, cancellationToken).ConfigureAwait(false);
        }

        LocationFile? read;

        await using (stream.ConfigureAwait(false))
        {
            read = await LocationFile.ReadAsync(stream, cancellationToken).ConfigureAwait(false);
        }

        if (read is null)
        {
            return await RaisedAsync(Refused, cancellationToken).ConfigureAwait(false);
        }

        copy.Hold(read);

        return Result.Success();
    }

    private async ValueTask<Result<ResolvedLocation?>> DegradedAsync(
        string scope,
        CancellationToken cancellationToken) =>
        (await RaisedAsync(scope, cancellationToken).ConfigureAwait(false))
        .Match(() => Result.Success<ResolvedLocation?>(null), Result.Failure<ResolvedLocation?>);

    private async ValueTask<Result> RaisedAsync(string scope, CancellationToken cancellationToken) =>
        await alerts
            .RaiseAsync(Alerts.Of(AlertCondition.Degradation, scope, time.GetUtcNow()), cancellationToken)
            .ConfigureAwait(false);
}
