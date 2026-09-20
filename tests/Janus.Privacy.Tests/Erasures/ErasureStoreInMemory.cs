using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Privacy.Erasures;

namespace Janus.Privacy.Tests.Erasures;

/// <summary>
/// The erasures, in a list, so a test can read back how far the host-side work got.
/// </summary>
internal sealed class ErasureStoreInMemory : IErasureStore
{
    private readonly List<Erasure> _erasures = [];

    /// <summary>
    /// What was written.
    /// </summary>
    public IReadOnlyList<Erasure> Erasures => _erasures;

    /// <summary>
    /// Writes one, as the erasure's own transaction does.
    /// </summary>
    /// <param name="erasure">The erasure.</param>
    public void Add(Erasure erasure) => _erasures.Add(erasure);

    /// <inheritdoc/>
    public ValueTask<Erasure?> FindBySubjectAsync(
        SubjectId subject,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult(_erasures.Find(erasure => erasure.Subject == subject));

    /// <inheritdoc/>
    public ValueTask<IReadOnlyList<Erasure>> FindIncompleteAsync(
        CancellationToken cancellationToken) =>
        ValueTask.FromResult<IReadOnlyList<Erasure>>(
        [
            .. _erasures
                .Where(erasure => erasure.Status is not ErasureStatus.Complete)
                .OrderBy(erasure => erasure.RequestedAt),
        ]);

    /// <inheritdoc/>
    public ValueTask RecordAsync(Erasure erasure, CancellationToken cancellationToken)
    {
        if (!_erasures.Contains(erasure))
        {
            throw new InvalidOperationException("The erasure has no row to record progress on.");
        }

        return ValueTask.CompletedTask;
    }
}
