using System;
using System.Collections.Generic;
using Janus.Authentication.Passwords;
using Janus.Core;

namespace Janus.Authentication.Registration;

/// <summary>
/// The whole of a registration before an account exists: what the steps have
/// collected, where they have reached, and nothing reserved anywhere else.
/// </summary>
/// <remarks>
/// Implements REG-SESS-001, REG-SESS-002, REG-SESS-006, REG-SESS-007 and
/// REG-PROF-002. The provisional subject identifier is drawn when the session is
/// created, so a WebAuthn ceremony run against the session uses the user handle the
/// account will carry; if the session is abandoned the identifier is simply never
/// used.
/// </remarks>
internal sealed class RegistrationSession
{
    private readonly List<StagedIdentity> _identifiers;
    private readonly List<StagedCredential> _credentials;

    private RegistrationSession(
        RegistrationSessionId id,
        SubjectId provisional,
        string client,
        string language,
        string source,
        DateTimeOffset createdAt,
        DateTimeOffset expiresAt,
        List<StagedIdentity> identifiers,
        List<StagedCredential> credentials)
    {
        Id = id;
        Provisional = provisional;
        Client = client;
        Language = language;
        Source = source;
        CreatedAt = createdAt;
        ExpiresAt = expiresAt;
        _identifiers = identifiers;
        _credentials = credentials;
    }

    /// <summary>Which session.</summary>
    public RegistrationSessionId Id { get; }

    /// <summary>
    /// The subject identifier the account will carry, which WebAuthn ceremonies use
    /// as the user handle before the account exists.
    /// </summary>
    public SubjectId Provisional { get; }

    /// <summary>
    /// The application the person came from, which decides where they are returned.
    /// </summary>
    public string Client { get; }

    /// <summary>The language the messages of this registration go out in.</summary>
    public string Language { get; }

    /// <summary>
    /// The address the registration was started from, which the source restrictions
    /// count against.
    /// </summary>
    public string Source { get; }

    /// <summary>When it was created.</summary>
    public DateTimeOffset CreatedAt { get; }

    /// <summary>When it is swept, leaving nothing.</summary>
    public DateTimeOffset ExpiresAt { get; }

    /// <summary>Where the registration has reached.</summary>
    public RegistrationStep Step { get; private set; } = RegistrationStep.Age;

    /// <summary>
    /// The date the age screen took, retained only where
    /// <c>profile.dateofbirth</c> is not off.
    /// </summary>
    public DateOnly? DateOfBirth { get; private set; }

    /// <summary>
    /// Whether the person was eighteen or over on the date the age screen took,
    /// derived and recorded with its timestamp.
    /// </summary>
    public bool? AdultAffirmed { get; private set; }

    /// <summary>
    /// The band the age screen recorded where the deployment takes no affirmation.
    /// </summary>
    public AgeGroup? Group { get; private set; }

    /// <summary>When the age screen was answered.</summary>
    public DateTimeOffset? AnsweredAgeAt { get; private set; }

    /// <summary>
    /// Whether an under-age date ended the session, after which it accepts no further
    /// date and reaches no identifier field.
    /// </summary>
    public bool AgeRefused { get; private set; }

    /// <summary>Every identifier staged, in the order it was staged.</summary>
    public IReadOnlyList<StagedIdentity> Identifiers => _identifiers;

    /// <summary>Every credential enrolled against the session.</summary>
    public IReadOnlyList<StagedCredential> Credentials => _credentials;

    /// <summary>The password hash staged, where a password has been set.</summary>
    public PasswordHash? Password { get; private set; }

    /// <summary>
    /// Whether the password staged reaches the single-factor floor, which is what
    /// decides whether a second step is mandatory.
    /// </summary>
    public bool PasswordStandsAlone { get; private set; }

    /// <summary>
    /// Whether the phone step was passed over, which the deployment has to permit.
    /// </summary>
    public bool PhoneSkipped { get; private set; }

    /// <summary>The version of the terms accepted, where they have been.</summary>
    public string? TermsVersion { get; private set; }

    /// <summary>The version of the privacy notice presented.</summary>
    public string? NoticeVersion { get; private set; }

    /// <summary>Whether the age screen has been answered.</summary>
    public bool AgeAnswered => AnsweredAgeAt is not null;

    /// <summary>
    /// Opens a registration session for one browser.
    /// </summary>
    /// <param name="id">The identifier issued for it.</param>
    /// <param name="provisional">The subject identifier the account will carry.</param>
    /// <param name="client">The originating application.</param>
    /// <param name="language">The language its messages go out in.</param>
    /// <param name="source">The address it was started from.</param>
    /// <param name="createdAt">When it was created.</param>
    /// <param name="lifetime">How long it lives before it is swept.</param>
    /// <returns>The session, at the age step with nothing collected.</returns>
    /// <exception cref="ArgumentNullException">A part is absent.</exception>
    public static RegistrationSession Open(
        RegistrationSessionId id,
        SubjectId provisional,
        string client,
        string language,
        string source,
        DateTimeOffset createdAt,
        TimeSpan lifetime)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(language);
        ArgumentNullException.ThrowIfNull(source);

        return new RegistrationSession(
            id,
            provisional,
            client,
            language,
            source,
            createdAt,
            createdAt + lifetime,
            [],
            []);
    }

    /// <summary>
    /// The session as it already stands, which is the store translating a row and no
    /// step the person took.
    /// </summary>
    /// <param name="id">Which session.</param>
    /// <param name="provisional">The subject identifier the account will carry.</param>
    /// <param name="client">The originating application.</param>
    /// <param name="language">The language its messages go out in.</param>
    /// <param name="source">The address it was started from.</param>
    /// <param name="createdAt">When it was created.</param>
    /// <param name="expiresAt">When it is swept.</param>
    /// <param name="identifiers">Every identifier staged.</param>
    /// <param name="credentials">Every credential enrolled against it.</param>
    /// <returns>The session, whose remaining state the caller carries onto it.</returns>
    /// <exception cref="ArgumentNullException">A part is absent.</exception>
    public static RegistrationSession Existing(
        RegistrationSessionId id,
        SubjectId provisional,
        string client,
        string language,
        string source,
        DateTimeOffset createdAt,
        DateTimeOffset expiresAt,
        IEnumerable<StagedIdentity> identifiers,
        IEnumerable<StagedCredential> credentials)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(language);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(identifiers);
        ArgumentNullException.ThrowIfNull(credentials);

        return new RegistrationSession(
            id,
            provisional,
            client,
            language,
            source,
            createdAt,
            expiresAt,
            [.. identifiers],
            [.. credentials]);
    }

    /// <summary>
    /// Carries the state a stored row holds beyond the identifiers and credentials
    /// back onto the session.
    /// </summary>
    /// <param name="step">Where the registration had reached.</param>
    /// <param name="dateOfBirth">The date retained, where it is.</param>
    /// <param name="adultAffirmed">The affirmation derived at the age step.</param>
    /// <param name="group">The band recorded, where one was.</param>
    /// <param name="answeredAgeAt">When the age screen was answered.</param>
    /// <param name="ageRefused">Whether an under-age date ended it.</param>
    /// <param name="password">The password hash staged.</param>
    /// <param name="passwordStandsAlone">Whether it reaches the single-factor floor.</param>
    /// <param name="phoneSkipped">Whether the phone step was passed over.</param>
    /// <param name="termsVersion">The terms version accepted.</param>
    /// <param name="noticeVersion">The notice version presented.</param>
    public void Restore(
        RegistrationStep step,
        DateOnly? dateOfBirth,
        bool? adultAffirmed,
        AgeGroup? group,
        DateTimeOffset? answeredAgeAt,
        bool ageRefused,
        PasswordHash? password,
        bool passwordStandsAlone,
        bool phoneSkipped,
        string? termsVersion,
        string? noticeVersion)
    {
        Step = step;
        DateOfBirth = dateOfBirth;
        AdultAffirmed = adultAffirmed;
        Group = group;
        AnsweredAgeAt = answeredAgeAt;
        AgeRefused = ageRefused;
        Password = password;
        PasswordStandsAlone = passwordStandsAlone;
        PhoneSkipped = phoneSkipped;
        TermsVersion = termsVersion;
        NoticeVersion = noticeVersion;
    }

    /// <summary>
    /// Whether the session has lapsed, after which it answers nothing.
    /// </summary>
    /// <param name="now">The instant to judge it at.</param>
    /// <returns>Whether it has.</returns>
    public bool HasExpired(DateTimeOffset now) => now >= ExpiresAt;

    /// <summary>
    /// Records the age answer and moves to the email step. The affirmation is derived
    /// here; the date is kept only where the deployment retains it.
    /// </summary>
    /// <param name="adult">Whether the date makes the person an adult.</param>
    /// <param name="retained">The date, where the deployment retains it.</param>
    /// <param name="group">The band, where the deployment records one.</param>
    /// <param name="at">When the screen was answered.</param>
    public void AnswerAge(bool adult, DateOnly? retained, AgeGroup? group, DateTimeOffset at)
    {
        AdultAffirmed = adult;
        DateOfBirth = retained;
        Group = group;
        AnsweredAgeAt = at;
        Step = RegistrationStep.Email;
    }

    /// <summary>
    /// Ends the session on an under-age date, which locks the screen against a second
    /// answer.
    /// </summary>
    /// <param name="at">When the screen was answered.</param>
    public void RefuseAge(DateTimeOffset at)
    {
        AgeRefused = true;
        AnsweredAgeAt = at;
    }

    /// <summary>
    /// Stages an identifier.
    /// </summary>
    /// <param name="identity">The staged identifier.</param>
    /// <exception cref="ArgumentNullException">It is absent.</exception>
    public void Stage(StagedIdentity identity)
    {
        ArgumentNullException.ThrowIfNull(identity);

        _identifiers.Add(identity);
    }

    /// <summary>
    /// Discards a staged identifier, which only an unverified extra one permits.
    /// </summary>
    /// <param name="identity">The staged identifier.</param>
    /// <exception cref="ArgumentNullException">It is absent.</exception>
    public void Discard(StagedIdentity identity)
    {
        ArgumentNullException.ThrowIfNull(identity);

        _ = _identifiers.Remove(identity);
    }

    /// <summary>
    /// One staged identifier by its identifier, or nothing where the session holds
    /// none such.
    /// </summary>
    /// <param name="id">Which staged identifier.</param>
    /// <returns>The staged identifier, or nothing.</returns>
    public StagedIdentity? Identity(IdentifierId id)
    {
        foreach (StagedIdentity staged in _identifiers)
        {
            if (staged.Id == id)
            {
                return staged;
            }
        }

        return null;
    }

    /// <summary>
    /// How many identifiers of one kind the session holds.
    /// </summary>
    /// <param name="kind">The kind.</param>
    /// <returns>How many.</returns>
    public int Count(IdentifierKind kind)
    {
        int held = 0;

        foreach (StagedIdentity staged in _identifiers)
        {
            if (staged.Kind == kind)
            {
                held++;
            }
        }

        return held;
    }

    /// <summary>
    /// Whether the session holds a verified identifier of a kind.
    /// </summary>
    /// <param name="kind">The kind.</param>
    /// <returns>Whether it does.</returns>
    public bool HasVerified(IdentifierKind kind)
    {
        foreach (StagedIdentity staged in _identifiers)
        {
            if (staged.Kind == kind && staged.IsVerified)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Whether every identifier the session holds is verified, which the confirm step
    /// completes only on.
    /// </summary>
    /// <returns>Whether they are.</returns>
    public bool EveryIdentifierVerified()
    {
        foreach (StagedIdentity staged in _identifiers)
        {
            if (!staged.IsVerified)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Records that the phone step was passed over.
    /// </summary>
    public void SkipPhone()
    {
        PhoneSkipped = true;
        Step = RegistrationStep.Confirm;
    }

    /// <summary>
    /// Moves to the step named, which the service has established is the next one.
    /// </summary>
    /// <param name="step">The step reached.</param>
    public void Reached(RegistrationStep step) => Step = step;

    /// <summary>
    /// Stages the password the security step set, replacing whatever was staged.
    /// </summary>
    /// <param name="hash">What the password hashes to.</param>
    /// <param name="standsAlone">Whether it reaches the single-factor floor.</param>
    /// <exception cref="ArgumentNullException">The hash is absent.</exception>
    public void SetPassword(PasswordHash hash, bool standsAlone)
    {
        ArgumentNullException.ThrowIfNull(hash);

        Password = hash;
        PasswordStandsAlone = standsAlone;
    }

    /// <summary>
    /// Stages a credential enrolled against the session.
    /// </summary>
    /// <param name="credential">The credential.</param>
    /// <exception cref="ArgumentNullException">It is absent.</exception>
    public void Enrol(StagedCredential credential)
    {
        ArgumentNullException.ThrowIfNull(credential);

        _credentials.Add(credential);
    }

    /// <summary>
    /// Records what the terms step accepted and presented.
    /// </summary>
    /// <param name="termsVersion">The version of the terms accepted.</param>
    /// <param name="noticeVersion">The version of the notice presented.</param>
    /// <exception cref="ArgumentNullException">Either is absent.</exception>
    public void AcceptTerms(string termsVersion, string noticeVersion)
    {
        ArgumentNullException.ThrowIfNull(termsVersion);
        ArgumentNullException.ThrowIfNull(noticeVersion);

        TermsVersion = termsVersion;
        NoticeVersion = noticeVersion;
    }
}
