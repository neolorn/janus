using System;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Core.Configuration;

namespace Janus.Hosting.Background;

/// <summary>
/// One piece of scheduled work: the named principal it runs as, how often it runs, and
/// what it does.
/// </summary>
/// <remarks>
/// Implements INF-BG-001 and INF-BG-002. A job is built from its principal and from
/// nothing else, and its work is handed the access context of that principal, so no
/// job reaches a store as nobody.
/// </remarks>
internal sealed class BackgroundJob
{
    private readonly DurationSetting? _cadence;

    private readonly TimeSpan _period;

    private readonly Func<IServiceProvider, AccessContext, CancellationToken, ValueTask<Result>> _work;

    private BackgroundJob(
        SystemPrincipal principal,
        DurationSetting? cadence,
        TimeSpan period,
        Func<IServiceProvider, AccessContext, CancellationToken, ValueTask<Result>> work)
    {
        Principal = principal;
        _cadence = cadence;
        _period = period;
        _work = work;
    }

    /// <summary>
    /// What the job is called, which is its principal's name.
    /// </summary>
    public string Name => Principal.Name;

    /// <summary>
    /// What the job runs as.
    /// </summary>
    public SystemPrincipal Principal { get; }

    /// <summary>
    /// How often the job runs where the setting that governs it cannot be read, which
    /// is that setting's default.
    /// </summary>
    public TimeSpan Fallback => _cadence?.Default ?? _period;

    /// <summary>
    /// A job that runs as often as a setting says.
    /// </summary>
    /// <param name="name">What it is called.</param>
    /// <param name="reason">Why it runs.</param>
    /// <param name="operation">The pool-wide operation it is.</param>
    /// <param name="cadence">The setting its interval is read from.</param>
    /// <param name="work">What it does.</param>
    /// <returns>The job.</returns>
    /// <exception cref="ArgumentNullException">The setting or the work is absent.</exception>
    /// <exception cref="ArgumentException">The name or the reason is absent or blank.</exception>
    public static BackgroundJob Every(
        string name,
        string reason,
        SystemOperation operation,
        DurationSetting cadence,
        Func<IServiceProvider, AccessContext, CancellationToken, ValueTask<Result>> work)
    {
        ArgumentNullException.ThrowIfNull(cadence);
        ArgumentNullException.ThrowIfNull(work);

        return new BackgroundJob(
            SystemPrincipal.ForDeployment(name, reason, operation),
            cadence,
            TimeSpan.Zero,
            work);
    }

    /// <summary>
    /// A job whose interval a chapter fixes rather than a setting.
    /// </summary>
    /// <param name="name">What it is called.</param>
    /// <param name="reason">Why it runs.</param>
    /// <param name="operation">The pool-wide operation it is.</param>
    /// <param name="period">How often it runs.</param>
    /// <param name="work">What it does.</param>
    /// <returns>The job.</returns>
    /// <exception cref="ArgumentNullException">The work is absent.</exception>
    /// <exception cref="ArgumentException">The name or the reason is absent or blank.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The period is not positive.</exception>
    public static BackgroundJob Every(
        string name,
        string reason,
        SystemOperation operation,
        TimeSpan period,
        Func<IServiceProvider, AccessContext, CancellationToken, ValueTask<Result>> work)
    {
        ArgumentNullException.ThrowIfNull(work);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(period, TimeSpan.Zero);

        return new BackgroundJob(
            SystemPrincipal.ForDeployment(name, reason, operation),
            cadence: null,
            period,
            work);
    }

    /// <summary>
    /// How often the job runs, read now, so a change to the setting takes effect at the
    /// job's next run.
    /// </summary>
    /// <param name="configuration">Where the setting is read.</param>
    /// <param name="cancellationToken">Abandons the read.</param>
    /// <returns>The interval, or the failure where the setting could not be read.</returns>
    /// <exception cref="ArgumentNullException">The store is absent.</exception>
    public async ValueTask<Result<TimeSpan>> IntervalAsync(
        IConfigurationStore configuration,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        return _cadence is null
            ? Result.Success(_period)
            : await configuration.ReadAsync(_cadence, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Runs the job once as its principal.
    /// </summary>
    /// <param name="services">The scope the run resolves what it uses from.</param>
    /// <param name="cancellationToken">Abandons the run.</param>
    /// <returns>Nothing, or the failure the work reported.</returns>
    /// <exception cref="ArgumentNullException">The scope is absent.</exception>
    public ValueTask<Result> RunAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(services);

        return _work(services, AccessContext.Of(Principal), cancellationToken);
    }
}
