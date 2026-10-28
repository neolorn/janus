using System;
using Janus.Core;
using Janus.Identity.Records;
using Xunit;

namespace Janus.Identity.Tests.Records;

/// <summary>
/// The one state a person record is in and the transitions that move it (REC-REC-002,
/// REC-ACC-001, REC-ATT-001, REC-TKD-001).
/// </summary>
[Trait("kind", "unit")]
public sealed class PersonRecordTests
{
    private static readonly SubjectId Guest = new(Guid.Parse("21111111-1111-4111-8111-111111111111"));
    private static readonly SubjectId Account = new(Guid.Parse("31111111-1111-4111-8111-111111111111"));
    private static readonly OrganizationId Shop = new(Guid.Parse("41111111-1111-4111-8111-111111111111"));
    private static readonly DateTimeOffset Noon = new(2026, 10, 28, 12, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// REC-REC-002 AC1: a record is created in a state, active, and has no suspended
    /// state to reach.
    /// </summary>
    [Fact]
    public void REC_REC_002_AC1_ARecordIsCreatedActive()
    {
        PersonRecord record = Collected();

        Assert.Equal(PersonRecordState.Active, record.State);
        Assert.Null(record.DeletingBy);
        Assert.Null(record.AttachedTo);
        Assert.DoesNotContain("Suspended", Enum.GetNames<PersonRecordState>());
    }

    /// <summary>
    /// REC-REC-002 AC2: a record's own deletion cancelled inside its window returns it
    /// active, or restricted where a restriction is held.
    /// </summary>
    [Fact]
    public void REC_REC_002_AC2_ACancelledDeletionReturnsTheRecordAsItWas()
    {
        PersonRecord active = Collected();
        active.RequestDeletion(DeletionOrigin.Self, Noon);
        active.CancelDeletion();

        PersonRecord restricted = Collected();
        restricted.Restrict();
        restricted.RequestDeletion(DeletionOrigin.OutOfBandRequest, Noon);
        restricted.CancelDeletion();

        Assert.Equal(PersonRecordState.Active, active.State);
        Assert.Null(active.DeletingBy);
        Assert.Equal(PersonRecordState.Restricted, restricted.State);
        Assert.False(restricted.RestrictionHeld);
    }

    /// <summary>
    /// REC-REC-002 AC2: a restriction asked while the record is deleting is held, and
    /// the record comes back restricted.
    /// </summary>
    [Fact]
    public void REC_REC_002_AC2_ARestrictionIsHeldThroughADeletionWindow()
    {
        PersonRecord record = Collected();
        record.RequestDeletion(DeletionOrigin.Self, Noon);

        record.HoldRestriction();
        record.CancelDeletion();

        Assert.Equal(PersonRecordState.Restricted, record.State);
    }

    /// <summary>
    /// REC-REC-002 AC2, REC-TKD-001: a reversal of its takedown returns the record to
    /// what it held at the trigger: the deletion it was in, with its origin and start,
    /// or the restriction.
    /// </summary>
    [Fact]
    public void REC_REC_002_AC2_AReversedTakedownReturnsWhatTheRecordHeld()
    {
        DateTimeOffset later = Noon.AddDays(2);
        PersonRecord deleting = Collected();
        deleting.RequestDeletion(DeletionOrigin.Self, Noon);
        deleting.Takedown(later);

        Assert.Equal(DeletionOrigin.Takedown, deleting.DeletingBy);
        Assert.Equal(later, deleting.DeletingSince);

        deleting.ReverseTakedown();

        PersonRecord restricted = Collected();
        restricted.Restrict();
        restricted.Takedown(later);
        restricted.ReverseTakedown();

        Assert.Equal(PersonRecordState.Deleting, deleting.State);
        Assert.Equal(DeletionOrigin.Self, deleting.DeletingBy);
        Assert.Equal(Noon, deleting.DeletingSince);
        Assert.Null(deleting.DeletionHeld);
        Assert.Equal(PersonRecordState.Restricted, restricted.State);
    }

    /// <summary>
    /// REC-REC-002 AC3: a transition the state does not admit is refused and changes
    /// nothing; a takedown is reversed, never cancelled.
    /// </summary>
    [Fact]
    public void REC_REC_002_AC3_ATransitionTheStateDoesNotAdmitIsRefused()
    {
        PersonRecord record = Collected();
        record.Takedown(Noon);

        Assert.Throws<InvalidOperationException>(record.CancelDeletion);
        Assert.Throws<InvalidOperationException>(() => record.Takedown(Noon));
        Assert.Throws<InvalidOperationException>(record.LiftRestriction);
        Assert.Throws<InvalidOperationException>(() => record.RequestDeletion(DeletionOrigin.Self, Noon));
        Assert.Throws<ArgumentOutOfRangeException>(() => Collected().RequestDeletion(DeletionOrigin.Takedown, Noon));
        Assert.Equal(PersonRecordState.Deleting, record.State);
        Assert.Equal(DeletionOrigin.Takedown, record.DeletingBy);
    }

    /// <summary>
    /// REC-REC-003, REC-ADM-003: an active record is erased at once, a deleting one at
    /// its window's end, and a deleted one is not erased again.
    /// </summary>
    [Fact]
    public void REC_REC_003_ARecordIsErasedFromAnyStateButDeleted()
    {
        PersonRecord active = Collected();
        active.MarkErased();

        PersonRecord deleting = Collected();
        deleting.Restrict();
        deleting.Takedown(Noon);
        deleting.MarkErased();

        Assert.Equal(PersonRecordState.Deleted, active.State);
        Assert.Equal(PersonRecordState.Deleted, deleting.State);
        Assert.False(deleting.RestrictionHeld);
        Assert.Throws<InvalidOperationException>(active.MarkErased);
    }

    /// <summary>
    /// REC-ACC-001: a code reaches a record attached to no account that is active,
    /// restricted, or in a window its own deletion began, and no other.
    /// </summary>
    [Fact]
    public void REC_ACC_001_ACodeReachesOnlyARecordInTheStatesItNames()
    {
        PersonRecord active = Collected();
        PersonRecord restricted = Collected();
        restricted.Restrict();
        PersonRecord own = Collected();
        own.RequestDeletion(DeletionOrigin.Self, Noon);
        PersonRecord outOfBand = Collected();
        outOfBand.RequestDeletion(DeletionOrigin.OutOfBandRequest, Noon);
        PersonRecord takenDown = Collected();
        takenDown.Takedown(Noon);
        PersonRecord attached = Collected();
        attached.Attach(Account, Noon);

        Assert.True(active.IsReachable);
        Assert.True(restricted.IsReachable);
        Assert.True(own.IsReachable);
        Assert.False(outOfBand.IsReachable);
        Assert.False(takenDown.IsReachable);
        Assert.False(attached.IsReachable);
    }

    /// <summary>
    /// REC-ATT-001: an attachment sets the account and its instant and nothing else, is
    /// made only to an active record, and is never undone or replaced.
    /// </summary>
    [Fact]
    public void REC_ATT_001_AnAttachmentKeepsTheRecordAndIsNeverReplaced()
    {
        PersonRecord record = Collected();
        PersonRecord restricted = Collected();
        restricted.Restrict();

        record.Attach(Account, Noon);

        Assert.Equal(Account, record.AttachedTo);
        Assert.Equal(Noon, record.AttachedAt);
        Assert.Equal(Guest, record.Subject);
        Assert.Equal(Shop, record.Organization);
        Assert.Throws<InvalidOperationException>(() => record.Attach(Guest, Noon));
        Assert.Throws<InvalidOperationException>(() => restricted.Attach(Account, Noon));
    }

    /// <summary>
    /// REC-REC-002: a restricted record is read and its rights exercised, and its own
    /// fields are not changed.
    /// </summary>
    [Fact]
    public void REC_REC_002_ARestrictedRecordsOwnFieldsAreNotChanged()
    {
        PersonRecord record = Collected();
        record.Restrict();

        Assert.True(DisplayName.TryParse("Layla", out DisplayName name));
        Assert.Throws<InvalidOperationException>(() => record.Rename(name));
        Assert.Throws<InvalidOperationException>(() => record.ChangeLanguage("en"));
        Assert.Null(record.Name);
        Assert.Equal("ar", record.Language);
    }

    /// <summary>
    /// REC-AGE-001, REC-COL-002: a record holds one form of the age answer, and keeps the
    /// first it was given.
    /// </summary>
    [Fact]
    public void REC_AGE_001_ARecordKeepsTheFirstAgeAnswerItWasGiven()
    {
        PersonRecord record = Collected();
        var affirmed = new RecordAgeAnswer(true, null, Noon, null);

        record.AnswerAge(affirmed);

        Assert.Equal(affirmed, record.Age);
        Assert.Throws<InvalidOperationException>(() => record.AnswerAge(new RecordAgeAnswer(null, AgeGroup.Adult, Noon, null)));
        Assert.Throws<ArgumentException>(() => Collected().AnswerAge(new RecordAgeAnswer(true, AgeGroup.Adult, Noon, null)));
        Assert.Throws<ArgumentException>(() => Collected().AnswerAge(new RecordAgeAnswer(null, null, Noon, null)));
        Assert.Equal(affirmed, record.Age);
    }

    private static PersonRecord Collected() =>
        PersonRecord.Create(Guest, Shop, PersonRecordOrigin.Collection, Noon, "ar");
}
