using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authorization.Gate;
using Janus.Core;

namespace Janus.Hosting.Tests.Authorization;

/// <summary>
/// The conditions the gate raised, held in memory.
/// </summary>
/// <remarks>Implements CONV-TEST-007: a fake, written by hand, never a mock.</remarks>
internal sealed class AccessAlertsInMemory : IAccessAlerts
{
    private readonly List<(AlertCondition Condition, string? Named)> _raised = [];

    /// <summary>
    /// Every condition raised, with whom it names, in the order it was.
    /// </summary>
    public IReadOnlyList<(AlertCondition Condition, string? Named)> Raised => _raised;

    /// <summary>
    /// The failure every raise answers, where a test sets one.
    /// </summary>
    public Error? Refuses { get; set; }

    /// <inheritdoc/>
    public ValueTask<Result> RaiseAsync(
        AlertCondition condition,
        string? scope,
        IReadOnlyDictionary<string, JsonElement> details,
        CancellationToken cancellationToken)
    {
        if (Refuses is Error refused)
        {
            return ValueTask.FromResult(Result.Failure(refused));
        }

        _raised.Add((condition, scope));

        return ValueTask.FromResult(Result.Success());
    }
}
