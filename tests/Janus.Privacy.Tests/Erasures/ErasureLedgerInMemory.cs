using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Privacy.Tests.Erasures;

/// <summary>
/// The off-host ledger, held as the lines it was given, and made to refuse them where
/// a case needs the storage unreachable.
/// </summary>
internal sealed class ErasureLedgerInMemory : IErasureLedger
{
    private readonly List<string> _lines = [];

    /// <summary>
    /// Every line appended, in order.
    /// </summary>
    public IReadOnlyList<string> Lines => _lines;

    /// <summary>
    /// Whether an append is made durable. A ledger that is not refuses the line.
    /// </summary>
    public bool Durable { get; set; } = true;

    /// <inheritdoc/>
    public ValueTask<Result> AppendAsync(string line, CancellationToken cancellationToken)
    {
        if (!Durable)
        {
            return ValueTask.FromResult(Result.Failure(Error.From(ErrorCodes.SystemFault)));
        }

        _lines.Add(line);

        return ValueTask.FromResult(Result.Success());
    }
}
