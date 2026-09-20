using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Privacy;

/// <summary>
/// Where a condition of the alert table is raised. The severity of a condition and
/// what it deduplicates under are the table's, so the area names the condition and
/// nothing else.
/// </summary>
/// <remarks>
/// Implements OPS-ALERT-001, OPS-ALERT-002, chapter 10 section 5.23 and
/// CONV-DESIGN-003.
/// </remarks>
internal interface IPrivacyAlerts
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
