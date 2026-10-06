using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Core.Configuration;

namespace Janus.Authorization.Gate;

/// <summary>
/// Watches the refusals recorded against each actor for a spike.
/// </summary>
/// <param name="audit">Where the refusals are counted.</param>
/// <param name="configuration">Where the threshold is read.</param>
/// <param name="alerts">Where a spike is raised.</param>
/// <remarks>
/// Implements AUTHZ-GATE-004, AUTHZ-CONCEAL-004 and OPS-ALERT-001 (D-153, D-183). The
/// count and the raise run in the transaction the refusal's record is written in, so the
/// alert and its <c>AlertRaised</c> row stand or fall with the record and never with the
/// caller's work. The windows are fixed and ten minutes long, counted from the Unix
/// epoch, so every instance of the library places a refusal in the same window. A system
/// principal's refusals are counted by its name, so one job's run neither hides in
/// another's nor raises it and the alert names the job; every other refusal is counted
/// by the acting subject it records, those recording the nil subject and no principal
/// together (D-183).
/// </remarks>
internal sealed class DenialSpikes(
    IAccessAudit audit,
    IConfigurationStore configuration,
    IAccessAlerts alerts)
{
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(10);

    /// <summary>
    /// Counts one recorded refusal against its actor's window and raises
    /// <see cref="AlertCondition.DenialSpike"/> once the window holds more than
    /// <c>alerting.denials.threshold</c>.
    /// </summary>
    /// <param name="denial">
    /// The refusal, already written in the transaction in progress, with its actor's
    /// refusals held.
    /// </param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>Nothing, or the failure where the spike could not be raised.</returns>
    /// <exception cref="ArgumentNullException">The refusal is absent.</exception>
    public async ValueTask<Result> WatchAsync(DeniedAccess denial, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(denial);

        int threshold = (await configuration
                .ReadAsync(Settings.AlertingDenialsThreshold, cancellationToken)
                .ConfigureAwait(false))
            .Match(read => read, error => throw new InvalidOperationException(error.Code.ToString()));

        DateTimeOffset opened = Opened(denial.At);

        int denials = await audit
            .CountAsync(denial.Acting ?? default, denial.Principal, opened, opened + Window, cancellationToken)
            .ConfigureAwait(false);

        if (denials <= threshold)
        {
            return Result.Success();
        }

        return await alerts
            .RaiseAsync(
                AlertCondition.DenialSpike,
                denial.Actor,
                new Dictionary<string, JsonElement>(StringComparer.Ordinal)
                {
                    ["denials"] = JsonSerializer.SerializeToElement(denials),
                },
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static DateTimeOffset Opened(DateTimeOffset at)
    {
        long elapsed = at.UtcTicks - DateTimeOffset.UnixEpoch.UtcTicks;

        return DateTimeOffset.UnixEpoch.AddTicks(elapsed - (elapsed % Window.Ticks));
    }
}
