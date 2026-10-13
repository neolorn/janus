using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Privacy.SubjectKeys;

namespace Janus.Privacy.Tests.SubjectKeys;

/// <summary>
/// Where a rotation's progress is kept, holding the latest one and no subject key, so a
/// test reads what a rotation decided and never what it re-wrapped.
/// </summary>
internal sealed class KeyRotationStoreInMemory : IKeyRotationStore
{
    /// <summary>
    /// Gets or sets the rotation standing, or nothing where none ever started.
    /// </summary>
    public KeyRotationProgress? Latest { get; set; }

    /// <summary>
    /// Gets the versions the stored values are wrapped under.
    /// </summary>
    public HashSet<int> Wrapping { get; } = [];

    /// <summary>
    /// Gets or sets what another run commits while this one waits for the progress, so
    /// a test may move the rotation under a decision about to be taken.
    /// </summary>
    public Action? Holding { get; set; }

    /// <inheritdoc/>
    public ValueTask<bool> UnderMaintenanceCredentialAsync(CancellationToken cancellationToken) =>
        ValueTask.FromResult(true);

    /// <inheritdoc/>
    public ValueTask HoldAsync(KeyRotationKind kind, CancellationToken cancellationToken)
    {
        Holding?.Invoke();

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask<KeyRotationProgress?> LatestAsync(KeyRotationKind kind, CancellationToken cancellationToken) =>
        ValueTask.FromResult(Latest is { } latest && latest.Kind == kind ? latest : null);

    /// <inheritdoc/>
    public ValueTask AddAsync(KeyRotationProgress progress, CancellationToken cancellationToken)
    {
        Latest = progress;

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask RecordAsync(KeyRotationProgress progress, CancellationToken cancellationToken)
    {
        Latest = progress;

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask<IReadOnlySet<int>> WrappingVersionsAsync(CancellationToken cancellationToken) =>
        ValueTask.FromResult<IReadOnlySet<int>>(Wrapping);

    /// <inheritdoc/>
    public ValueTask<KeyRotationBatch> ReWrapSubjectKeysAfterAsync(
        SubjectKeyId? after,
        int count,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult(new KeyRotationBatch(Last: null, Processed: 0));

    /// <inheritdoc/>
    public ValueTask<int> ReWrapRemainingSubjectKeysAsync(int count, CancellationToken cancellationToken) =>
        ValueTask.FromResult(0);
}
