using System.Threading;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Privacy.Records;

namespace Janus.Privacy.Tests.Records;

/// <summary>
/// The three supplied fields of the records of processing, as a test set them.
/// </summary>
internal sealed class ComplianceStoreInMemory : IComplianceStore
{
    /// <summary>
    /// What the deployment has stated, which is nothing until it states something.
    /// </summary>
    public ComplianceRecord Held { get; private set; } = new(null, null, []);

    /// <inheritdoc/>
    public ValueTask<ComplianceRecord> ReadAsync(CancellationToken cancellationToken) =>
        ValueTask.FromResult(Held);

    /// <inheritdoc/>
    public ValueTask RecordAsync(ComplianceRecord record, CancellationToken cancellationToken)
    {
        Held = record;

        return ValueTask.CompletedTask;
    }
}
