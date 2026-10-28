using System;
using Janus.Authentication.Records;
using Janus.Core;
using Xunit;

namespace Janus.Authentication.Tests.Records;

/// <summary>
/// The collection session's own rules: the age before any contact where it is asked,
/// one contact of each kind, and the lock an under-age date leaves (REC-COL-001,
/// REC-AGE-001).
/// </summary>
[Trait("kind", "unit")]
public sealed class CollectionSessionTests
{
    private const string Number = "+441632960011";

    private static readonly DateTimeOffset Noon = new(2026, 10, 28, 12, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// REC-COL-001: a session lives its lifetime from its creation and no longer.
    /// </summary>
    [Fact]
    public void REC_COL_001_ASessionLivesItsLifetimeFromItsCreation()
    {
        CollectionSession session = Opened(asksAge: false);

        Assert.Equal(Noon.AddHours(1), session.ExpiresAt);
        Assert.False(session.HasExpired(Noon.AddMinutes(59)));
        Assert.True(session.HasExpired(Noon.AddHours(1)));
    }

    /// <summary>
    /// REC-COL-001 AC1: with the age asked, no contact is staged before it is answered.
    /// </summary>
    [Fact]
    public void REC_COL_001_AC1_NoContactIsStagedBeforeTheAgeTheSessionAsks()
    {
        CollectionSession session = Opened(asksAge: true);

        Assert.False(session.TakesContacts);
        Assert.Throws<InvalidOperationException>(() => session.Stage(IdentifierKind.Phone, Number));

        session.AnswerAge(adult: true, retained: null, group: null, Noon);
        session.Stage(IdentifierKind.Phone, Number);

        Assert.Equal(IdentifierKind.Phone, Assert.Single(session.Contacts).Kind);
    }

    /// <summary>
    /// REC-COL-001 AC1, REC-AGE-001 AC2: an under-age date locks the session against a
    /// second answer and takes no contact.
    /// </summary>
    [Fact]
    public void REC_COL_001_AC1_AnUnderAgeDateLocksTheSession()
    {
        CollectionSession session = Opened(asksAge: true);

        session.RefuseAge(Noon);

        Assert.True(session.AgeRefused);
        Assert.False(session.AgeAnswered);
        Assert.False(session.TakesContacts);
        Assert.Throws<InvalidOperationException>(() => session.AnswerAge(adult: true, retained: null, group: null, Noon));
        Assert.Throws<InvalidOperationException>(() => session.Stage(IdentifierKind.Email, "person@example.test"));
    }

    /// <summary>
    /// REC-COL-001: an age the session does not ask, or already holds, is not taken.
    /// </summary>
    [Fact]
    public void REC_COL_001_AnAgeTheSessionDoesNotAskOrHoldsIsNotTaken()
    {
        CollectionSession unasked = Opened(asksAge: false);
        CollectionSession answered = Opened(asksAge: true);

        answered.AnswerAge(adult: null, retained: null, AgeGroup.Adult, Noon);

        Assert.Throws<InvalidOperationException>(() => unasked.AnswerAge(adult: true, retained: null, group: null, Noon));
        Assert.Throws<InvalidOperationException>(() => answered.AnswerAge(adult: true, retained: null, group: null, Noon));
        Assert.Equal(AgeGroup.Adult, answered.Group);
    }

    /// <summary>
    /// REC-COL-001, REC-REC-001: a contact staged again replaces the one of its kind,
    /// unverified, and the session holds at most one of each kind.
    /// </summary>
    [Fact]
    public void REC_COL_001_AContactStagedAgainReplacesTheOneOfItsKind()
    {
        CollectionSession session = Opened(asksAge: false);

        session.Stage(IdentifierKind.Phone, "+441632960099");
        session.Verify(IdentifierKind.Phone, Noon);
        session.Stage(IdentifierKind.Email, "person@example.test");
        session.Stage(IdentifierKind.Phone, Number);

        Assert.Equal(2, session.Contacts.Count);
        Assert.Equal(new CollectedContact(IdentifierKind.Phone, Number, VerifiedAt: null), session.Contact(IdentifierKind.Phone));
    }

    /// <summary>
    /// REC-COL-001: a collection takes an email or a phone, and a code verifies only a
    /// contact staged.
    /// </summary>
    [Fact]
    public void REC_COL_001_ASessionTakesAnEmailOrAPhoneAndVerifiesOnlyWhatItStaged()
    {
        CollectionSession session = Opened(asksAge: false);

        Assert.Throws<ArgumentException>(() => session.Stage(IdentifierKind.Username, "person"));
        Assert.Throws<InvalidOperationException>(() => session.Verify(IdentifierKind.Email, Noon));

        session.Stage(IdentifierKind.Email, "person@example.test");
        session.Verify(IdentifierKind.Email, Noon);

        Assert.True(session.Contact(IdentifierKind.Email)!.IsVerified);
    }

    private static CollectionSession Opened(bool asksAge) =>
        CollectionSession.Open(
            CollectionSessionId.New(TimeProvider.System),
            asksAge,
            "en",
            Noon,
            TimeSpan.FromHours(1));
}
