using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Authentication.Tests.Accounts;

/// <summary>
/// The records each context was given, counted in memory.
/// </summary>
/// <remarks>Implements OPS-ALERT-005 for the tests.</remarks>
internal sealed class ReadVolumeInMemory : IReadVolume
{
    private readonly List<(AccessContext Context, int Records)> _returned = [];

    /// <summary>
    /// Every count reported, in order.
    /// </summary>
    public IReadOnlyList<(AccessContext Context, int Records)> Returned => _returned;

    /// <inheritdoc/>
    public ValueTask<Result> ReturnedAsync(AccessContext context, int records, CancellationToken cancellationToken)
    {
        _returned.Add((context, records));

        return ValueTask.FromResult(Result.Success());
    }
}
