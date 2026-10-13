using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Janus.Privacy.SubjectKeys;

namespace Janus.Privacy.Tests.SubjectKeys;

/// <summary>
/// Where the fingerprints a rotation computes again are kept, holding none, so a test
/// reads what a rotation decided and never what it computed.
/// </summary>
internal sealed class FingerprintRotationStoreInMemory : IFingerprintRotationStore
{
    /// <summary>
    /// Gets the versions the fingerprints still read stand under.
    /// </summary>
    public HashSet<int> Computing { get; } = [];

    /// <summary>
    /// Gets or sets how many fingerprints nothing can compute again stand under a
    /// previous version.
    /// </summary>
    public int Standing { get; set; }

    /// <summary>
    /// Gets how many times what stood under a previous version was forgotten.
    /// </summary>
    public int Forgotten { get; private set; }

    /// <inheritdoc/>
    public ValueTask<IReadOnlySet<int>> FingerprintVersionsAsync(
        DateTimeOffset now,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult<IReadOnlySet<int>>(Computing);

    /// <inheritdoc/>
    public ValueTask<KeyRotationBatch> RecomputeSubjectsAfterAsync(
        SubjectKeyId? after,
        int count,
        DateTimeOffset now,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult(new KeyRotationBatch(Last: null, Processed: 0));

    /// <inheritdoc/>
    public ValueTask<int> RecomputeRemainingAsync(int count, DateTimeOffset now, CancellationToken cancellationToken) =>
        ValueTask.FromResult(0);

    /// <inheritdoc/>
    public ValueTask<int> StandingAsync(DateTimeOffset now, CancellationToken cancellationToken) =>
        ValueTask.FromResult(Standing);

    /// <inheritdoc/>
    public ValueTask ForgetAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        Forgotten++;

        return ValueTask.CompletedTask;
    }
}
