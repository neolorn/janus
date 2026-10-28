using System;
using Janus.Core;

namespace Janus.Identity.Records;

/// <summary>
/// A person whose personal data a host keeps in their own right and who holds no
/// account: a data subject of its own, with its own subject identifier, data key and
/// consents, and no way to sign in.
/// </summary>
/// <remarks>
/// Implements REC-REC-001, REC-REC-002, REC-ATT-001 and chapter 10 sections 5.12b and
/// 5.51. A record is in exactly one state and reaches the next only through the
/// transition named for it; the row is never removed, so its erasure is state and key
/// destruction. Its transitions are an account's without the suspension, which a record
/// has no sign-in to need: a takedown takes it straight into its window.
/// </remarks>
internal sealed class PersonRecord
{
    private PersonRecord(
        SubjectId subject,
        OrganizationId organization,
        PersonRecordOrigin origin,
        DateTimeOffset createdAt,
        string language)
    {
        Subject = subject;
        Organization = organization;
        Origin = origin;
        CreatedAt = createdAt;
        Language = language;
        State = PersonRecordState.Active;
    }

    /// <summary>
    /// The record's subject identifier, drawn from the one space accounts' are.
    /// </summary>
    public SubjectId Subject { get; }

    /// <summary>
    /// The organization the host keeps the data in, named at its writing and never
    /// changed.
    /// </summary>
    public OrganizationId Organization { get; }

    /// <summary>
    /// Whether the person gave the data in a host's screen or staff entered it.
    /// </summary>
    public PersonRecordOrigin Origin { get; }

    /// <summary>
    /// When the record was created.
    /// </summary>
    public DateTimeOffset CreatedAt { get; }

    /// <summary>
    /// The language every message to the record is sent in, one of the deployment's
    /// message languages.
    /// </summary>
    public string Language { get; private set; }

    /// <summary>
    /// The one state the record is in.
    /// </summary>
    public PersonRecordState State { get; private set; }

    /// <summary>
    /// Whether a restriction of processing is held while the record is in a deletion
    /// window, so that it is in force again when the record returns (PRIV-RIGHT-004).
    /// </summary>
    public bool RestrictionHeld { get; private set; }

    /// <summary>
    /// Why the record entered its grace window, where it is deleting.
    /// </summary>
    public DeletionOrigin? DeletingBy { get; private set; }

    /// <summary>
    /// When the grace window began, where it is running.
    /// </summary>
    public DateTimeOffset? DeletingSince { get; private set; }

    /// <summary>
    /// The origin of the deletion a takedown found running, which its reversal returns
    /// the record to.
    /// </summary>
    public DeletionOrigin? DeletionHeld { get; private set; }

    /// <summary>
    /// When the deletion a takedown found running began.
    /// </summary>
    public DateTimeOffset? DeletionHeldSince { get; private set; }

    /// <summary>
    /// The account the record is attached to, where it is.
    /// </summary>
    public SubjectId? AttachedTo { get; private set; }

    /// <summary>
    /// When the record was attached, where it is.
    /// </summary>
    public DateTimeOffset? AttachedAt { get; private set; }

    /// <summary>
    /// The name the record holds, where it holds one: a personal field under its key.
    /// </summary>
    public DisplayName? Name { get; private set; }

    /// <summary>
    /// The answer to the age question, where one was given (REC-AGE-001).
    /// </summary>
    public RecordAgeAnswer? Age { get; private set; }

    /// <summary>
    /// Whether a code may reach the record: attached to no account, and active,
    /// restricted, or in a deletion window its own deletion began (REC-ACC-001).
    /// </summary>
    public bool IsReachable =>
        AttachedTo is null
        && (State is PersonRecordState.Active or PersonRecordState.Restricted
            || (State is PersonRecordState.Deleting && DeletingBy is DeletionOrigin.Self));

    /// <summary>
    /// Creates a record. It is created active.
    /// </summary>
    /// <param name="subject">The identifier issued for it.</param>
    /// <param name="organization">The organization the host keeps the data in.</param>
    /// <param name="origin">Whether the person gave the data or staff entered it.</param>
    /// <param name="createdAt">The instant it was created.</param>
    /// <param name="language">The language messages to it are sent in.</param>
    /// <returns>The record.</returns>
    /// <exception cref="ArgumentException">The language is blank.</exception>
    public static PersonRecord Create(
        SubjectId subject,
        OrganizationId organization,
        PersonRecordOrigin origin,
        DateTimeOffset createdAt,
        string language)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(language);

        return new(subject, organization, origin, createdAt, language);
    }

    /// <summary>
    /// The record as it already stands. This is the store's translation of a stored
    /// row and no transition, so it takes the state it is given.
    /// </summary>
    /// <param name="subject">The identifier the record was issued.</param>
    /// <param name="organization">The organization it belongs to.</param>
    /// <param name="origin">How it was written.</param>
    /// <param name="createdAt">The instant it was created.</param>
    /// <param name="language">The language messages to it are sent in.</param>
    /// <param name="state">The state it is in.</param>
    /// <param name="restrictionHeld">Whether a restriction is held through a window.</param>
    /// <param name="deletingBy">Why its grace window began, where one is running.</param>
    /// <param name="deletingSince">When that window began.</param>
    /// <param name="deletionHeld">The origin of the deletion its takedown holds.</param>
    /// <param name="deletionHeldSince">When that held deletion began.</param>
    /// <param name="attachedTo">The account it is attached to, where it is.</param>
    /// <param name="attachedAt">When it was attached.</param>
    /// <param name="name">The name it holds, where it holds one.</param>
    /// <param name="age">The answer to the age question, where one was given.</param>
    /// <returns>The record.</returns>
    public static PersonRecord Existing(
        SubjectId subject,
        OrganizationId organization,
        PersonRecordOrigin origin,
        DateTimeOffset createdAt,
        string language,
        PersonRecordState state,
        bool restrictionHeld,
        DeletionOrigin? deletingBy,
        DateTimeOffset? deletingSince,
        DeletionOrigin? deletionHeld,
        DateTimeOffset? deletionHeldSince,
        SubjectId? attachedTo,
        DateTimeOffset? attachedAt,
        DisplayName? name,
        RecordAgeAnswer? age) =>
        new(subject, organization, origin, createdAt, language)
        {
            State = state,
            RestrictionHeld = restrictionHeld,
            DeletingBy = deletingBy,
            DeletingSince = deletingSince,
            DeletionHeld = deletionHeld,
            DeletionHeldSince = deletionHeldSince,
            AttachedTo = attachedTo,
            AttachedAt = attachedAt,
            Name = name,
            Age = age,
        };

    /// <summary>
    /// Changes the language messages to the record are sent in.
    /// </summary>
    /// <param name="language">The language.</param>
    /// <exception cref="ArgumentException">The language is blank.</exception>
    /// <exception cref="InvalidOperationException">The record is not active.</exception>
    public void ChangeLanguage(string language)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(language);
        Require(PersonRecordState.Active);

        Language = language;
    }

    /// <summary>
    /// Sets or clears the name the record holds.
    /// </summary>
    /// <param name="name">The name, or nothing to clear it.</param>
    /// <exception cref="InvalidOperationException">The record is not active.</exception>
    public void Rename(DisplayName? name)
    {
        Require(PersonRecordState.Active);

        Name = name;
    }

    /// <summary>
    /// Records the person's answer to the age question. A record keeps the first answer
    /// it was given; a later collection does not replace it (REC-COL-002).
    /// </summary>
    /// <param name="answer">The answer.</param>
    /// <exception cref="ArgumentException">The answer holds both forms, or neither.</exception>
    /// <exception cref="InvalidOperationException">
    /// The record is not active, or already holds an answer.
    /// </exception>
    public void AnswerAge(RecordAgeAnswer answer)
    {
        ArgumentNullException.ThrowIfNull(answer);

        if (!answer.IsWellFormed)
        {
            throw new ArgumentException("An age answer holds the affirmation or the age group.", nameof(answer));
        }

        Require(PersonRecordState.Active);

        if (Age is not null)
        {
            throw new InvalidOperationException("A record keeps the age answer it was first given.");
        }

        Age = answer;
    }

    /// <summary>
    /// Restricts processing at the person's request.
    /// </summary>
    /// <exception cref="InvalidOperationException">The record is not active.</exception>
    public void Restrict()
    {
        Require(PersonRecordState.Active);

        State = PersonRecordState.Restricted;
    }

    /// <summary>
    /// Restricts processing while the record is in its deletion window: the restriction
    /// is held, and the record comes back restricted.
    /// </summary>
    /// <exception cref="InvalidOperationException">The record is not deleting.</exception>
    public void HoldRestriction()
    {
        Require(PersonRecordState.Deleting);

        RestrictionHeld = true;
    }

    /// <summary>
    /// Lifts the restriction.
    /// </summary>
    /// <exception cref="InvalidOperationException">The record is not restricted.</exception>
    public void LiftRestriction()
    {
        Require(PersonRecordState.Restricted);

        State = PersonRecordState.Active;
    }

    /// <summary>
    /// Starts the deletion grace window, at the person's own request or on a request
    /// that arrived out of band. A restriction in force is held through it.
    /// </summary>
    /// <param name="by">Whether the person asked or a request arrived out of band.</param>
    /// <param name="at">The instant the window began.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The origin is the takedown's, which enters the window through its own transition.
    /// </exception>
    /// <exception cref="InvalidOperationException">The record is neither active nor restricted.</exception>
    public void RequestDeletion(DeletionOrigin by, DateTimeOffset at)
    {
        if (by is DeletionOrigin.Takedown)
        {
            throw new ArgumentOutOfRangeException(
                nameof(by),
                by,
                "A takedown enters the window through its own transition.");
        }

        Require(PersonRecordState.Active, PersonRecordState.Restricted);

        RestrictionHeld = State is PersonRecordState.Restricted;
        EnterDeletion(by, at);
    }

    /// <summary>
    /// Triggers a takedown: the record enters the grace window as a takedown's, holding
    /// a running deletion with its origin and start, and the only way back is the
    /// reversal.
    /// </summary>
    /// <param name="at">The instant the takedown was triggered.</param>
    /// <exception cref="InvalidOperationException">
    /// The record is already taken down, or deleted.
    /// </exception>
    public void Takedown(DateTimeOffset at)
    {
        Require(PersonRecordState.Active, PersonRecordState.Restricted, PersonRecordState.Deleting);

        if (State is PersonRecordState.Deleting)
        {
            if (DeletingBy is DeletionOrigin.Takedown)
            {
                throw new InvalidOperationException("The record is already taken down.");
            }

            DeletionHeld = DeletingBy;
            DeletionHeldSince = DeletingSince;
        }

        RestrictionHeld = RestrictionHeld || State is PersonRecordState.Restricted;
        EnterDeletion(DeletionOrigin.Takedown, at);
    }

    /// <summary>
    /// Cancels a deletion inside its window and restores the record, restricted where a
    /// restriction is held. A takedown is not cancellable this way.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// The record is not deleting, or its window was entered by a takedown.
    /// </exception>
    public void CancelDeletion()
    {
        Require(PersonRecordState.Deleting);

        if (DeletingBy is DeletionOrigin.Takedown)
        {
            throw new InvalidOperationException("A takedown is reversed, never cancelled.");
        }

        LeaveDeletion();
    }

    /// <summary>
    /// Reverses a takedown inside its window: the record returns to the deletion it was
    /// in, with its origin and start; else restricted where a restriction is held; else
    /// active.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// The record is not deleting, or its window was not entered by a takedown.
    /// </exception>
    public void ReverseTakedown()
    {
        Require(PersonRecordState.Deleting);

        if (DeletingBy is not DeletionOrigin.Takedown)
        {
            throw new InvalidOperationException("Only a takedown is reversed.");
        }

        if (DeletionHeld is DeletionOrigin by && DeletionHeldSince is DateTimeOffset since)
        {
            DeletingBy = by;
            DeletingSince = since;
            DeletionHeld = null;
            DeletionHeldSince = null;

            return;
        }

        LeaveDeletion();
    }

    /// <summary>
    /// Records that the erasure transaction has run: at the end of a window, or at once
    /// for a record the host keeps nothing about any longer (REC-ADM-003). The row
    /// stays; its personal fields are unreadable because the key is gone.
    /// </summary>
    /// <exception cref="InvalidOperationException">The record is already deleted.</exception>
    public void MarkErased()
    {
        Require(PersonRecordState.Active, PersonRecordState.Restricted, PersonRecordState.Deleting);

        State = PersonRecordState.Deleted;
        RestrictionHeld = false;
        DeletionHeld = null;
        DeletionHeldSince = null;
    }

    /// <summary>
    /// Records an erasure again after a restore to a point before it took the erasure
    /// away (DR-016). The record is erased from whatever state the restore left it in.
    /// </summary>
    /// <exception cref="InvalidOperationException">The record is already deleted.</exception>
    public void ReapplyErasure() => MarkErased();

    /// <summary>
    /// Attaches the record to an account that proved now a contact it holds. The record
    /// keeps its subject, key, consents and organization; nothing is moved.
    /// </summary>
    /// <param name="account">The account it is attached to.</param>
    /// <param name="at">The instant of the attachment.</param>
    /// <exception cref="InvalidOperationException">
    /// The record is not active, or is already attached.
    /// </exception>
    public void Attach(SubjectId account, DateTimeOffset at)
    {
        Require(PersonRecordState.Active);

        if (AttachedTo is not null)
        {
            throw new InvalidOperationException("An attachment is never undone or replaced.");
        }

        AttachedTo = account;
        AttachedAt = at;
    }

    private void EnterDeletion(DeletionOrigin by, DateTimeOffset at)
    {
        State = PersonRecordState.Deleting;
        DeletingBy = by;
        DeletingSince = at;
    }

    // Chapter 10 section 5.12b: a held restriction comes back as restricted.
    private void LeaveDeletion()
    {
        State = RestrictionHeld ? PersonRecordState.Restricted : PersonRecordState.Active;
        RestrictionHeld = false;
        DeletingBy = null;
        DeletingSince = null;
    }

    private void Require(params PersonRecordState[] states)
    {
        if (Array.IndexOf(states, State) < 0)
        {
            throw new InvalidOperationException(
                "A person record in " + State + " does not make this transition.");
        }
    }
}
