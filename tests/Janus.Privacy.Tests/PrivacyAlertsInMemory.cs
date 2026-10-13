using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Privacy.Tests;

/// <summary>
/// The alert seam, keeping what the area raised so a test can read it back.
/// </summary>
internal sealed class PrivacyAlertsInMemory : IPrivacyAlerts
{
    private readonly List<PrivacyAlertRaised> _raised = [];

    /// <summary>
    /// What was raised, in the order it was.
    /// </summary>
    public IReadOnlyList<PrivacyAlertRaised> Raised => _raised;

    /// <summary>
    /// What a raise answers with instead of writing its row, where a test stands in
    /// for a row that cannot be written.
    /// </summary>
    public Error? Refusal { get; set; }

    /// <inheritdoc/>
    public ValueTask<Result> RaiseAsync(
        AlertCondition condition,
        string? scope,
        IReadOnlyDictionary<string, JsonElement> details,
        CancellationToken cancellationToken)
    {
        if (Refusal is Error refused)
        {
            return ValueTask.FromResult(Result.Failure(refused));
        }

        _raised.Add(new PrivacyAlertRaised(condition, scope, details));

        return ValueTask.FromResult(Result.Success());
    }
}
