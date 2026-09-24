using System;
using Janus.Core;

namespace Janus.Privacy.SubjectKeys;

/// <summary>
/// How far one rotation has gone: the version it rotates to, the last subject whose key
/// it has passed, how many it has re-wrapped, and when it started, completed and retired
/// the versions before it.
/// </summary>
/// <remarks>
/// Implements OPS-SEC-003. The row is written in the transaction of the batch it
/// records, so a run that stops for any reason resumes after the last batch that
/// committed and never re-wraps what that batch did.
/// </remarks>
internal sealed class KeyRotationProgress
{
    private KeyRotationProgress(
        KeyRotationKind kind,
        int version,
        SubjectId? lastSubject,
        int processed,
        DateTimeOffset startedAt,
        DateTimeOffset? completedAt,
        DateTimeOffset? retiredAt)
    {
        Kind = kind;
        Version = version;
        LastSubject = lastSubject;
        Processed = processed;
        StartedAt = startedAt;
        CompletedAt = completedAt;
        RetiredAt = retiredAt;
    }

    /// <summary>
    /// Which key is rotated.
    /// </summary>
    public KeyRotationKind Kind { get; }

    /// <summary>
    /// The version the rotation re-wraps under.
    /// </summary>
    public int Version { get; }

    /// <summary>
    /// The last subject whose key the ordered pass has reached, or nothing before the
    /// first batch.
    /// </summary>
    public SubjectId? LastSubject { get; private set; }

    /// <summary>
    /// How many subject keys the rotation has re-wrapped.
    /// </summary>
    public int Processed { get; private set; }

    /// <summary>
    /// When the rotation started.
    /// </summary>
    public DateTimeOffset StartedAt { get; }

    /// <summary>
    /// When the re-wrap reported every value under the new version.
    /// </summary>
    public DateTimeOffset? CompletedAt { get; private set; }

    /// <summary>
    /// When the operator confirmed the escrow copy sealed and the versions before this
    /// one were retired.
    /// </summary>
    public DateTimeOffset? RetiredAt { get; private set; }

    /// <summary>
    /// A rotation starting.
    /// </summary>
    /// <param name="kind">Which key is rotated.</param>
    /// <param name="version">The version it re-wraps under.</param>
    /// <param name="at">When it started.</param>
    /// <returns>The rotation, with nothing yet re-wrapped.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The version is not a version.</exception>
    public static KeyRotationProgress Started(KeyRotationKind kind, int version, DateTimeOffset at)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(version, 1);

        return new KeyRotationProgress(kind, version, lastSubject: null, processed: 0, at, completedAt: null, retiredAt: null);
    }

    /// <summary>
    /// The rotation as it already stands. This is the store's translation of a stored
    /// row and no operation.
    /// </summary>
    /// <param name="kind">Which key is rotated.</param>
    /// <param name="version">The version it re-wraps under.</param>
    /// <param name="lastSubject">The last subject the ordered pass reached.</param>
    /// <param name="processed">How many subject keys it has re-wrapped.</param>
    /// <param name="startedAt">When it started.</param>
    /// <param name="completedAt">When it completed.</param>
    /// <param name="retiredAt">When it retired the versions before it.</param>
    /// <returns>The rotation.</returns>
    public static KeyRotationProgress Existing(
        KeyRotationKind kind,
        int version,
        SubjectId? lastSubject,
        int processed,
        DateTimeOffset startedAt,
        DateTimeOffset? completedAt,
        DateTimeOffset? retiredAt) =>
        new(kind, version, lastSubject, processed, startedAt, completedAt, retiredAt);

    /// <summary>
    /// Records a batch of the ordered pass.
    /// </summary>
    /// <param name="lastSubject">The last subject the batch reached.</param>
    /// <param name="reWrapped">How many keys it re-wrapped.</param>
    /// <exception cref="InvalidOperationException">The rotation has completed.</exception>
    public void Passed(SubjectId lastSubject, int reWrapped)
    {
        Counted(reWrapped);
        LastSubject = lastSubject;
    }

    /// <summary>
    /// Records keys re-wrapped outside the ordered pass: ones written under a previous
    /// version behind the point it had reached.
    /// </summary>
    /// <param name="reWrapped">How many keys were re-wrapped.</param>
    /// <exception cref="InvalidOperationException">The rotation has retired.</exception>
    public void Swept(int reWrapped)
    {
        if (RetiredAt is not null)
        {
            throw new InvalidOperationException("A retired rotation re-wraps nothing.");
        }

        ArgumentOutOfRangeException.ThrowIfNegative(reWrapped);
        Processed += reWrapped;
    }

    /// <summary>
    /// Records that every value is under the new version.
    /// </summary>
    /// <param name="at">When.</param>
    /// <exception cref="InvalidOperationException">The rotation has already completed.</exception>
    public void Complete(DateTimeOffset at)
    {
        if (CompletedAt is not null)
        {
            throw new InvalidOperationException("The rotation has already completed.");
        }

        CompletedAt = at;
    }

    /// <summary>
    /// Records that the escrow copy is sealed and the versions before this one retired.
    /// </summary>
    /// <param name="at">When.</param>
    /// <exception cref="InvalidOperationException">
    /// The rotation has not completed, or has already retired.
    /// </exception>
    public void Retire(DateTimeOffset at)
    {
        if (CompletedAt is null || RetiredAt is not null)
        {
            throw new InvalidOperationException("Only a completed rotation retires, and only once.");
        }

        RetiredAt = at;
    }

    private void Counted(int reWrapped)
    {
        if (CompletedAt is not null)
        {
            throw new InvalidOperationException("A completed rotation has no ordered pass left.");
        }

        ArgumentOutOfRangeException.ThrowIfNegative(reWrapped);
        Processed += reWrapped;
    }
}
