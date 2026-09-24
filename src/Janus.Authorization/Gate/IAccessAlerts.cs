using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Authorization.Gate;

/// <summary>
/// Where a condition of the alert table the gate observes is raised. The severity of a
/// condition and what it deduplicates under are the table's, so the gate names the
/// condition and nothing else.
/// </summary>
/// <remarks>
/// Implements OPS-ALERT-001, AUTHZ-GATE-004 and CONV-DESIGN-003. The router every
/// condition goes through is identity's; this port is the narrow view of it the gate
/// needs, because an area project reaches no other area project (CONV-LAYOUT-001).
/// </remarks>
internal interface IAccessAlerts
{
    /// <summary>
    /// Raises one condition.
    /// </summary>
    /// <param name="condition">Which condition.</param>
    /// <param name="scope">Whose, or nothing where the row names no one.</param>
    /// <param name="details">The structured detail of the row.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of raising it.</returns>
    ValueTask RaiseAsync(
        AlertCondition condition,
        string? scope,
        IReadOnlyDictionary<string, JsonElement> details,
        CancellationToken cancellationToken);
}
