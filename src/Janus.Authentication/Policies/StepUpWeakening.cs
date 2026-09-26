using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Janus.Authentication.Alerting;
using Janus.Core;
using Janus.Core.Configuration;

namespace Janus.Authentication.Policies;

/// <summary>
/// The alert a policy change raises where it leaves a step-up gate asking less.
/// </summary>
/// <remarks>
/// Implements OPS-ALERT-001 and D-083. A gate that asks less is the same lever as an
/// alert destination under another name: whoever holds one stepped-up administrative
/// session could lower every gate an attacker would meet next, so the change is told as
/// it is made rather than found in the trail after suspicion.
/// </remarks>
internal static class StepUpWeakening
{
    /// <summary>
    /// The alert for a change that weakened the gates named.
    /// </summary>
    /// <param name="key">The policy key the change was written to.</param>
    /// <param name="scope">The organization whose policy it is, or nothing for the system's.</param>
    /// <param name="gates">The step-up actions whose gate asks less.</param>
    /// <param name="at">When the change was made.</param>
    /// <returns>The alert to raise.</returns>
    /// <exception cref="ArgumentNullException">The gates are absent.</exception>
    public static AlertRaised Of(
        ConfigurationKey key,
        OrganizationId? scope,
        IReadOnlyList<StepUpAction> gates,
        DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(gates);

        var details = new Dictionary<string, JsonElement>(StringComparer.Ordinal)
        {
            ["key"] = JsonSerializer.SerializeToElement(key.ToString()),
            ["gates"] = JsonSerializer.SerializeToElement(gates.Select(WrittenName.Of).ToArray()),
        };

        return Alerts.Of(AlertCondition.StepUpPolicyWeakened, scope?.ToString(), at, details);
    }
}
