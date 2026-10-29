using System;
using System.Collections.Generic;
using System.Linq;
using Janus.Core;

namespace Janus.Authentication.Records;

/// <summary>
/// A collection session: the age answer and the contacts a host's own screen collects
/// for a person record, staged until the host writes the record.
/// </summary>
/// <remarks>
/// Implements REC-COL-001 and REC-AGE-001. The session reserves nothing and writes into
/// no other table; an expired or abandoned one is swept and leaves nothing behind. It
/// holds at most one contact of each kind, since a record does (REC-REC-001).
/// </remarks>
internal sealed class CollectionSession
{
    private readonly List<CollectedContact> _contacts;

    private CollectionSession(
        CollectionSessionId id,
        bool asksAge,
        string language,
        DateTimeOffset createdAt,
        DateTimeOffset expiresAt,
        IEnumerable<CollectedContact> contacts)
    {
        Id = id;
        AsksAge = asksAge;
        Language = language;
        CreatedAt = createdAt;
        ExpiresAt = expiresAt;
        _contacts = [.. contacts];
    }

    /// <summary>
    /// The session's identifier.
    /// </summary>
    public CollectionSessionId Id { get; }

    /// <summary>
    /// Whether the host's screen asks the age question before any contact (REC-AGE-001).
    /// </summary>
    public bool AsksAge { get; }

    /// <summary>
    /// The language the browser asked in, which a code is sent in.
    /// </summary>
    public string Language { get; }

    /// <summary>
    /// When the session was created.
    /// </summary>
    public DateTimeOffset CreatedAt { get; }

    /// <summary>
    /// When it stops answering, which is its creation and the configured lifetime.
    /// </summary>
    public DateTimeOffset ExpiresAt { get; }

    /// <summary>
    /// Whether the date makes the person an adult, where the deployment takes an
    /// affirmation and the age was answered.
    /// </summary>
    public bool? AdultAffirmed { get; private set; }

    /// <summary>
    /// The age group, where the deployment records one and the age was answered.
    /// </summary>
    public AgeGroup? Group { get; private set; }

    /// <summary>
    /// The date of birth, where the deployment retains it.
    /// </summary>
    [NeverLogged]
    public DateOnly? DateOfBirth { get; private set; }

    /// <summary>
    /// When the age question was answered, or refused.
    /// </summary>
    public DateTimeOffset? AnsweredAgeAt { get; private set; }

    /// <summary>
    /// Whether an under-age date ended the session, which locks it against a second
    /// answer and takes no contact.
    /// </summary>
    public bool AgeRefused { get; private set; }

    /// <summary>
    /// Whether the session holds an age answer it can pass to a record.
    /// </summary>
    public bool AgeAnswered => AnsweredAgeAt is not null && !AgeRefused;

    /// <summary>
    /// Whether a contact may be staged: the age is asked no longer, or was answered.
    /// </summary>
    public bool TakesContacts => !AsksAge || AgeAnswered;

    /// <summary>
    /// The contacts staged, at most one of each kind.
    /// </summary>
    public IReadOnlyList<CollectedContact> Contacts => _contacts;

    /// <summary>
    /// Opens a session.
    /// </summary>
    /// <param name="id">Its identifier.</param>
    /// <param name="asksAge">Whether the screen asks the age question.</param>
    /// <param name="language">The language the browser asked in.</param>
    /// <param name="createdAt">When it opens.</param>
    /// <param name="lifetime">How long it lives.</param>
    /// <returns>The session.</returns>
    /// <exception cref="ArgumentNullException">The language is absent.</exception>
    public static CollectionSession Open(
        CollectionSessionId id,
        bool asksAge,
        string language,
        DateTimeOffset createdAt,
        TimeSpan lifetime)
    {
        ArgumentNullException.ThrowIfNull(language);

        return new CollectionSession(id, asksAge, language, createdAt, createdAt + lifetime, []);
    }

    /// <summary>
    /// A session as it was stored.
    /// </summary>
    /// <param name="id">Its identifier.</param>
    /// <param name="asksAge">Whether the screen asks the age question.</param>
    /// <param name="language">The language the browser asked in.</param>
    /// <param name="createdAt">When it opened.</param>
    /// <param name="expiresAt">When it stops answering.</param>
    /// <param name="contacts">What it staged.</param>
    /// <returns>The session.</returns>
    /// <exception cref="ArgumentNullException">A part is absent.</exception>
    public static CollectionSession Existing(
        CollectionSessionId id,
        bool asksAge,
        string language,
        DateTimeOffset createdAt,
        DateTimeOffset expiresAt,
        IEnumerable<CollectedContact> contacts)
    {
        ArgumentNullException.ThrowIfNull(language);
        ArgumentNullException.ThrowIfNull(contacts);

        return new CollectionSession(id, asksAge, language, createdAt, expiresAt, contacts);
    }

    /// <summary>
    /// Carries the stored age answer back onto a session read from the store.
    /// </summary>
    /// <param name="adult">Whether the date made the person an adult.</param>
    /// <param name="group">The age group.</param>
    /// <param name="dateOfBirth">The date, where retained.</param>
    /// <param name="answeredAt">When the question was answered.</param>
    /// <param name="refused">Whether the answer ended the session.</param>
    public void Restore(
        bool? adult,
        AgeGroup? group,
        DateOnly? dateOfBirth,
        DateTimeOffset? answeredAt,
        bool refused)
    {
        AdultAffirmed = adult;
        Group = group;
        DateOfBirth = dateOfBirth;
        AnsweredAgeAt = answeredAt;
        AgeRefused = refused;
    }

    /// <summary>
    /// Whether the session has lapsed.
    /// </summary>
    /// <param name="now">The current instant.</param>
    /// <returns>Whether it has.</returns>
    public bool HasExpired(DateTimeOffset now) => now >= ExpiresAt;

    /// <summary>
    /// Records the age answer.
    /// </summary>
    /// <param name="adult">
    /// Whether the date makes the person an adult, and nothing where the deployment
    /// takes no affirmation.
    /// </param>
    /// <param name="retained">The date, where the deployment retains it.</param>
    /// <param name="group">The age group, where the deployment records one.</param>
    /// <param name="at">When the question was answered.</param>
    /// <exception cref="InvalidOperationException">
    /// The session does not ask the age, or holds an answer already.
    /// </exception>
    public void AnswerAge(bool? adult, DateOnly? retained, AgeGroup? group, DateTimeOffset at)
    {
        Asked();

        AdultAffirmed = adult;
        DateOfBirth = retained;
        Group = group;
        AnsweredAgeAt = at;
    }

    /// <summary>
    /// Ends the session on an under-age date, which locks it against a second answer.
    /// </summary>
    /// <param name="at">When the question was answered.</param>
    /// <exception cref="InvalidOperationException">
    /// The session does not ask the age, or holds an answer already.
    /// </exception>
    public void RefuseAge(DateTimeOffset at)
    {
        Asked();

        AgeRefused = true;
        AnsweredAgeAt = at;
    }

    /// <summary>
    /// Stages a contact, replacing the one of its kind staged before.
    /// </summary>
    /// <param name="kind">An email or a phone.</param>
    /// <param name="value">Its canonical value.</param>
    /// <exception cref="ArgumentNullException">The value is absent.</exception>
    /// <exception cref="ArgumentException">The kind is neither an email nor a phone.</exception>
    /// <exception cref="InvalidOperationException">The session takes no contact yet.</exception>
    public void Stage(IdentifierKind kind, string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        if (kind is not (IdentifierKind.Email or IdentifierKind.Phone))
        {
            throw new ArgumentException("A collection takes an email or a phone.", nameof(kind));
        }

        if (!TakesContacts)
        {
            throw new InvalidOperationException("The age the session asks comes before any contact.");
        }

        _ = _contacts.RemoveAll(contact => contact.Kind == kind);
        _contacts.Add(new CollectedContact(kind, value, VerifiedAt: null));
    }

    /// <summary>
    /// The contact of a kind, where one is staged.
    /// </summary>
    /// <param name="kind">Which kind.</param>
    /// <returns>The contact, or nothing.</returns>
    public CollectedContact? Contact(IdentifierKind kind) =>
        _contacts.FirstOrDefault(contact => contact.Kind == kind);

    /// <summary>
    /// Marks the contact of a kind verified by its code.
    /// </summary>
    /// <param name="kind">Which kind.</param>
    /// <param name="at">When the code verified it.</param>
    /// <exception cref="InvalidOperationException">No contact of the kind is staged.</exception>
    public void Verify(IdentifierKind kind, DateTimeOffset at)
    {
        int index = _contacts.FindIndex(contact => contact.Kind == kind);

        if (index < 0)
        {
            throw new InvalidOperationException("No contact of the kind is staged.");
        }

        _contacts[index] = _contacts[index] with { VerifiedAt = at };
    }

    private void Asked()
    {
        if (!AsksAge || AnsweredAgeAt is not null)
        {
            throw new InvalidOperationException("The session does not ask the age, or holds an answer already.");
        }
    }
}
