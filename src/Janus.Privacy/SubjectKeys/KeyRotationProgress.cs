using System;
using Janus.Core;

namespace Janus.Privacy.SubjectKeys;

/// <summary>
/// How far one rotation has gone: the version it rotates to, the last subject it has
/// passed, how many values it has moved to that version (subject keys re-wrapped, or
/// fingerprints computed again), and when it started, completed and retired the versions
/// before it.
/// </summary>
/// <remarks>
/// Implements OPS-SEC-003. The row is written in the transaction of the batch it
/// records, so a run that stops for any reason resumes after the last batch that
/// committed and never moves again what that batch did.
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
    /// The version the rotation moves every value to.
    /// </summary>
    public int Version { get; }

    /// <summary>
    /// The last subject whose key the ordered pass has reached, or nothing before the
    /// first batch.
    /// </summary>
    public SubjectId? LastSubject { get; private set; }

    /// <summary>
    /// How many values the rotation has moved to the version.
    /// </summary>
    public int Processed { get; private set; }

    /// <summary>
    /// When the rotation started.
    /// </summary>
    public DateTimeOffset StartedAt { get; }

    /// <summary>
    /// When the rotation reported every value under the new version.
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
    /// <param name="version">The version it moves every value to.</param>
    /// <param name="at">When it started.</param>
    /// <returns>The rotation, with nothing yet moved.</returns>
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
    /// <param name="version">The version it moves every value to.</param>
    /// <param name="lastSubject">The last subject the ordered pass reached.</param>
    /// <param name="processed">How many values it has moved to the version.</param>
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
    /// <param name="moved">How many values it moved to the version.</param>
    /// <exception cref="InvalidOperationException">The rotation has completed.</exception>
    public void Passed(SubjectId lastSubject, int moved)
    {
        Counted(moved);
        LastSubject = lastSubject;
    }

    /// <summary>
    /// Records values moved outside the ordered pass: ones written under a previous
    /// version behind the point it had reached.
    /// </summary>
    /// <param name="moved">How many values were moved to the version.</param>
    /// <exception cref="InvalidOperationException">The rotation has retired.</exception>
    public void Swept(int moved)
    {
        if (RetiredAt is not null)
        {
            throw new InvalidOperationException("A retired rotation moves nothing.");
        }

        ArgumentOutOfRangeException.ThrowIfNegative(moved);
        Processed += moved;
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

    private void Counted(int moved)
    {
        if (CompletedAt is not null)
        {
            throw new InvalidOperationException("A completed rotation has no ordered pass left.");
        }

        ArgumentOutOfRangeException.ThrowIfNegative(moved);
        Processed += moved;
    }
}
