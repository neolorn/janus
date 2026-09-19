using System;
using Janus.Core;
using Janus.Privacy.Erasures;
using Xunit;

namespace Janus.Privacy.Tests.Erasures;

/// <summary>
/// What an erasure records once the library's own work has committed (IDN-LIFE-003b).
/// </summary>
/// <remarks>
/// The row never describes a step of the library's, which has committed if the row
/// exists. It describes the host-side work still outstanding.
/// </remarks>
[Trait("kind", "unit")]
public sealed class ErasureTests
{
    private static readonly DateTimeOffset Noon = new(2026, 9, 19, 12, 0, 0, TimeSpan.Zero);

    private static readonly SubjectId Ahmed =
        new(Guid.Parse("11111111-1111-4111-8111-111111111111"));

    /// <summary>
    /// IDN-LIFE-003b: an erasure begins awaiting its subscribers, with no attempt yet
    /// made, and carries why it happened.
    /// </summary>
    [Fact]
    public void IDN_LIFE_003b_AnErasureBeginsAwaitingItsSubscribers()
    {
        var erasure = Erasure.Begun(Ahmed, Noon, ErasureReason.ErasureRequest);

        Assert.Equal(Ahmed, erasure.Subject);
        Assert.Equal(Noon, erasure.RequestedAt);
        Assert.Equal(ErasureReason.ErasureRequest, erasure.Reason);
        Assert.Equal(ErasureStatus.AwaitingSubscribers, erasure.Status);
        Assert.Equal(0, erasure.Attempts);
    }

    /// <summary>
    /// IDN-LIFE-003b: attempts are counted across subscribers while the work is
    /// outstanding.
    /// </summary>
    [Fact]
    public void IDN_LIFE_003b_AttemptsAreCountedAcrossSubscribers()
    {
        var erasure = Erasure.Begun(Ahmed, Noon, ErasureReason.ErasureRequest);

        erasure.RecordAttempt();
        erasure.RecordAttempt();

        Assert.Equal(2, erasure.Attempts);
    }

    /// <summary>
    /// IDN-LIFE-003b: <c>complete</c> means every required subscriber confirmed, and
    /// nothing follows it.
    /// </summary>
    [Fact]
    public void IDN_LIFE_003b_ACompletedErasureTakesNoFurtherProgress()
    {
        var erasure = Erasure.Begun(Ahmed, Noon, ErasureReason.ErasureRequest);
        erasure.Complete();

        Assert.Equal(ErasureStatus.Complete, erasure.Status);
        Assert.Throws<InvalidOperationException>(erasure.RecordAttempt);
        Assert.Throws<InvalidOperationException>(erasure.Fail);
        Assert.Throws<InvalidOperationException>(erasure.Complete);
    }

    /// <summary>
    /// IDN-LIFE-003b: <c>failed</c> means a subscriber exhausted its retries, and the
    /// manual completion path is what closes it.
    /// </summary>
    [Fact]
    public void IDN_LIFE_003b_AFailedErasureIsClosedByTheManualPath()
    {
        var erasure = Erasure.Begun(Ahmed, Noon, ErasureReason.MinorTakedown);
        erasure.RecordAttempt();
        erasure.Fail();

        Assert.Equal(ErasureStatus.Failed, erasure.Status);

        erasure.CompleteManually();

        Assert.Equal(ErasureStatus.Complete, erasure.Status);
    }

    /// <summary>
    /// The manual completion path exists for an erasure a subscriber failed, and for no
    /// other.
    /// </summary>
    [Fact]
    public void CompleteManually_AnErasureThatNeverFailed_Throws()
    {
        var erasure = Erasure.Begun(Ahmed, Noon, ErasureReason.OrganizationErasure);

        Assert.Throws<InvalidOperationException>(erasure.CompleteManually);
    }

    /// <summary>
    /// An erasure read back from a row carries the progress that was stored and is no
    /// progress that was just made.
    /// </summary>
    [Fact]
    public void Existing_AStoredRow_CarriesTheProgressItHeld()
    {
        var erasure = Erasure.Existing(
            Ahmed,
            Noon,
            ErasureReason.OrganizationErasure,
            ErasureStatus.Failed,
            attempts: 5);

        Assert.Equal(ErasureStatus.Failed, erasure.Status);
        Assert.Equal(5, erasure.Attempts);
    }

    /// <summary>
    /// A count of attempts is never negative.
    /// </summary>
    [Fact]
    public void Existing_ANegativeAttemptCount_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Erasure.Existing(
            Ahmed,
            Noon,
            ErasureReason.ErasureRequest,
            ErasureStatus.AwaitingSubscribers,
            attempts: -1));
    }
}
