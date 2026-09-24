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
/// Implements AUTHZ-GATE-004 and OPS-ALERT-001 (D-153). The windows are fixed and ten
/// minutes long, counted from the Unix epoch, so every instance of the library places a
/// refusal in the same window. Every refusal that names no actor is counted together,
/// so a run of them is raised like any actor's.
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
    /// <param name="denial">The refusal, already recorded.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of counting it.</returns>
    /// <exception cref="ArgumentNullException">The refusal is absent.</exception>
    public async ValueTask WatchAsync(DeniedAccess denial, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(denial);

        // An unreadable threshold does not silence the condition: the default stands.
        int threshold = (await configuration
                .ReadAsync(Settings.AlertingDenialsThreshold, cancellationToken)
                .ConfigureAwait(false))
            .Match(read => read, _ => Settings.AlertingDenialsThreshold.Default);

        DateTimeOffset opened = Opened(denial.At);

        int denials = await audit
            .CountAsync(denial.Acting, opened, opened + Window, cancellationToken)
            .ConfigureAwait(false);

        if (denials > threshold)
        {
            await alerts
                .RaiseAsync(
                    AlertCondition.DenialSpike,
                    denial.Acting?.ToString(),
                    new Dictionary<string, JsonElement>(StringComparer.Ordinal)
                    {
                        ["denials"] = JsonSerializer.SerializeToElement(denials),
                    },
                    cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private static DateTimeOffset Opened(DateTimeOffset at)
    {
        long elapsed = at.UtcTicks - DateTimeOffset.UnixEpoch.UtcTicks;

        return DateTimeOffset.UnixEpoch.AddTicks(elapsed - (elapsed % Window.Ticks));
    }
}
