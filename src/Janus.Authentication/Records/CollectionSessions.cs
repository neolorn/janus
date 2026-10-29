using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Factors;
using Janus.Authentication.Registration;
using Janus.Authentication.Sending;
using Janus.Core;
using Janus.Core.Configuration;

namespace Janus.Authentication.Records;

/// <summary>
/// The collection session's steps, which its endpoints map: the session's creation,
/// the age, each contact and its code, and the end (REC-COL-001).
/// </summary>
/// <param name="sessions">Where the sessions are held.</param>
/// <param name="codes">The verification codes a contact is proved by.</param>
/// <param name="sending">The governed send a code goes out through.</param>
/// <param name="restrictions">What releases a send's count once its code is answered.</param>
/// <param name="throttle">The delays of AUTH-ABUSE-001.</param>
/// <param name="configuration">The deployment's settings.</param>
/// <param name="work">The unit of work every step writes through.</param>
/// <param name="time">The deployment's clock.</param>
/// <remarks>
/// Implements REC-COL-001, REC-AGE-001, AUTH-FACT-004 and AUTH-ABUSE-001. The session is
/// the browser's, as a registration session is, so its steps are no operation of the
/// contract and take no access context (chapter 07, D-221). Nothing is written outside
/// the session but the records of the abuse controls, the verification codes of its
/// contacts and the messages sent, and the steps answer the same whoever holds a value.
/// </remarks>
internal sealed class CollectionSessions(
    ICollectionSessionStore sessions,
    VerificationCodes codes,
    IGovernedSend sending,
    ISendingRestrictions restrictions,
    ThrottleService throttle,
    IConfigurationStore configuration,
    IUnitOfWork work,
    TimeProvider time)
{
    /// <summary>
    /// Creates a collection session.
    /// </summary>
    /// <param name="signedIn">Who is signed in on the browser, or nothing.</param>
    /// <param name="asksAge">Whether the host's screen asks the age question first.</param>
    /// <param name="language">The language the browser asked in.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// The new session's state, or <c>identity.record.signedin</c> where the browser
    /// holds a live sign-in session, nothing staged.
    /// </returns>
    /// <exception cref="ArgumentNullException">The language is absent.</exception>
    public async ValueTask<Result<CollectionState>> BeginAsync(
        AccessContext? signedIn,
        bool asksAge,
        string language,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(language);

        // REC-COL-001 AC4: a person who holds an account gives their data under it.
        if (signedIn is not null)
        {
            return Result.Failure<CollectionState>(Error.From(ErrorCodes.RecordSignedIn));
        }

        Error? failure = null;

        TimeSpan lifetime = (await configuration
                .ReadAsync(Settings.RecordsCollectionLifetime, cancellationToken).ConfigureAwait(false))
            .Match(value => value, error => Held<TimeSpan>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure<CollectionState>(failure);
        }

        var session = CollectionSession.Open(
            CollectionSessionId.New(time),
            asksAge,
            language,
            time.GetUtcNow(),
            lifetime);

        if ((await work.BeginAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(_ => null, error => error) is Error notBegun)
        {
            return Result.Failure<CollectionState>(notBegun);
        }

        await sessions.AddAsync(session, cancellationToken).ConfigureAwait(false);

        return (await work.CommitAsync(cancellationToken).ConfigureAwait(false))
            .Match(() => Result.Success(CollectionState.Of(session)), Result.Failure<CollectionState>);
    }

    /// <summary>
    /// What a session holds.
    /// </summary>
    /// <param name="id">Which session.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>Its state, or <c>auth.session.expired</c> where it has ended.</returns>
    public async ValueTask<Result<CollectionState>> StateAsync(
        CollectionSessionId id,
        CancellationToken cancellationToken) =>
        await LiveAsync(id, cancellationToken).ConfigureAwait(false) is CollectionSession live
            ? Result.Success(CollectionState.Of(live))
            : Result.Failure<CollectionState>(Error.From(ErrorCodes.SessionExpired));

    /// <summary>
    /// Answers the age question the session asks, as the registration's age screen is
    /// answered (REG-PROF-002, REC-AGE-001).
    /// </summary>
    /// <param name="id">Which session.</param>
    /// <param name="dateOfBirth">The date entered.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// Success, or <c>auth.session.expired</c>; <c>identity.profile.underage</c> where an
    /// under-age date ends the session on an adults-only deployment, the end being the
    /// refusal's kept write, and for every date after it;
    /// <c>identity.collection.incomplete</c> where the session does not ask the age or
    /// holds an answer.
    /// </returns>
    public ValueTask<Result> AnswerAgeAsync(
        CollectionSessionId id,
        DateOnly dateOfBirth,
        CancellationToken cancellationToken) =>
        HeldAsync(id, live => AgeAsync(live, dateOfBirth, cancellationToken), keeps: true, cancellationToken);

    /// <summary>
    /// Stages a contact and sends a code to it, replacing the contact of its kind
    /// staged before.
    /// </summary>
    /// <param name="id">Which session.</param>
    /// <param name="kind">An email or a phone.</param>
    /// <param name="value">The value entered.</param>
    /// <param name="source">The address the request came from.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// Success, the same whoever holds the value; or <c>auth.session.expired</c>,
    /// <c>identity.collection.incomplete</c> before the age the session asks,
    /// <c>auth.throttled</c>, <c>identity.identifier.invalid</c>, or the governed send's
    /// refusal, nothing staged.
    /// </returns>
    /// <exception cref="ArgumentNullException">The value or the source is absent.</exception>
    public ValueTask<Result> StageAsync(
        CollectionSessionId id,
        IdentifierKind kind,
        string value,
        string source,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(source);

        return HeldAsync(
            id,
            live => StagedAsync(live, kind, value, source, cancellationToken),
            keeps: false,
            cancellationToken);
    }

    /// <summary>
    /// Verifies the staged contact of a kind by its code (AUTH-FACT-004).
    /// </summary>
    /// <param name="id">Which session.</param>
    /// <param name="kind">Which contact.</param>
    /// <param name="code">The code entered.</param>
    /// <param name="source">The address the request came from.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// Success, or <c>auth.session.expired</c>, <c>auth.throttled</c>,
    /// <c>auth.code.invalid</c>, or <c>auth.code.expired</c> for a code past its life or
    /// its attempts and for a kind the session holds no contact of.
    /// </returns>
    /// <exception cref="ArgumentNullException">The code or the source is absent.</exception>
    public ValueTask<Result> VerifyAsync(
        CollectionSessionId id,
        IdentifierKind kind,
        [NeverLogged] string code,
        string source,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(code);
        ArgumentNullException.ThrowIfNull(source);

        return HeldAsync(
            id,
            live => VerifiedAsync(live, kind, code, source, cancellationToken),
            keeps: true,
            cancellationToken);
    }

    /// <summary>
    /// Ends a session and the codes its contacts had outstanding.
    /// </summary>
    /// <param name="id">Which session, or nothing where the browser carries none.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>Success, whether or not a session was there to end.</returns>
    public async ValueTask<Result> AbandonAsync(
        CollectionSessionId? id,
        CancellationToken cancellationToken)
    {
        if (id is not CollectionSessionId named)
        {
            return Result.Success();
        }

        if ((await work.BeginAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(_ => null, error => error) is Error notBegun)
        {
            return Result.Failure(notBegun);
        }

        await EndAsync(named, cancellationToken).ConfigureAwait(false);

        return await work.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Ends a session inside the caller's transaction: the codes its contacts had
    /// outstanding go with it.
    /// </summary>
    /// <param name="id">Which session.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of ending it.</returns>
    public async ValueTask EndAsync(CollectionSessionId id, CancellationToken cancellationToken)
    {
        if (await sessions.FindForUpdateAsync(id, cancellationToken).ConfigureAwait(false)
            is CollectionSession held)
        {
            foreach (CollectedContact contact in held.Contacts)
            {
                await codes.EndAsync(Holder(held.Id, contact.Kind), cancellationToken).ConfigureAwait(false);
            }

            await sessions.RemoveAsync(held.Id, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// The session an identifier names, read under its lock, where it has not lapsed.
    /// </summary>
    /// <param name="id">Which session.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The session, or nothing.</returns>
    /// <exception cref="InvalidOperationException">No transaction is open.</exception>
    public async ValueTask<CollectionSession?> HoldAsync(
        CollectionSessionId id,
        CancellationToken cancellationToken)
    {
        CollectionSession? held = await sessions.FindForUpdateAsync(id, cancellationToken).ConfigureAwait(false);

        return held is null || held.HasExpired(time.GetUtcNow()) ? null : held;
    }

    // What a contact's code is held under: the session and the kind, so a contact
    // staged again replaces the code of the one before it. It is a digest of seventeen
    // bytes, so it is never the holder of a registration's identifier, which is one of
    // thirty-two.
    private static byte[] Holder(CollectionSessionId session, IdentifierKind kind)
    {
        Span<byte> named = stackalloc byte[17];

        _ = session.Value.TryWriteBytes(named);
        named[16] = (byte)kind;

        return SHA256.HashData(named);
    }

    private async ValueTask<(Result Answer, bool Commits)> AgeAsync(
        CollectionSession? live,
        DateOnly dateOfBirth,
        CancellationToken cancellationToken)
    {
        if (live is null)
        {
            return (Gone(), false);
        }

        // REC-COL-001 AC1: the end locks the session against a second date, so a person
        // cannot walk the date forward until it passes (REG-PROF-002).
        if (live.AgeRefused)
        {
            return (Result.Failure(Error.From(ErrorCodes.ProfileUnderage)), false);
        }

        if (!live.AsksAge || live.AnsweredAgeAt is not null)
        {
            return (OutOfOrder(), false);
        }

        Error? failure = null;

        AttributeRequirement affirmation = (await configuration
                .ReadAsync(Settings.RegistrationAdultAffirmation, cancellationToken).ConfigureAwait(false))
            .Match(value => value, error => Held<AttributeRequirement>(error, ref failure));

        AttributeRequirement retention = (await configuration
                .ReadAsync(Settings.ProfileDateOfBirth, cancellationToken).ConfigureAwait(false))
            .Match(value => value, error => Held<AttributeRequirement>(error, ref failure));

        if (failure is not null)
        {
            return (Result.Failure(failure), false);
        }

        DateTimeOffset now = time.GetUtcNow();
        bool adult = RegistrationService.IsAdult(dateOfBirth, DateOnly.FromDateTime(now.UtcDateTime));

        // CONV-DESIGN-003: the end is the refusal's kept write.
        if (affirmation is not AttributeRequirement.Off && !adult)
        {
            live.RefuseAge(now);

            await sessions.RecordAsync(live, cancellationToken).ConfigureAwait(false);

            return (Result.Failure(Error.From(ErrorCodes.ProfileUnderage)), true);
        }

        live.AnswerAge(
            affirmation is AttributeRequirement.Off ? null : adult,
            retention is AttributeRequirement.Off ? null : dateOfBirth,
            affirmation is AttributeRequirement.Off ? RegistrationService.Band(adult) : null,
            now);

        await sessions.RecordAsync(live, cancellationToken).ConfigureAwait(false);

        return (Result.Success(), true);
    }

    private async ValueTask<(Result Answer, bool Commits)> StagedAsync(
        CollectionSession? live,
        IdentifierKind kind,
        string value,
        string source,
        CancellationToken cancellationToken)
    {
        if (live is null)
        {
            return (Gone(), false);
        }

        // REC-COL-001 AC1: no contact is taken before the age the session asks.
        if (!live.TakesContacts)
        {
            return (OutOfOrder(), false);
        }

        string entered = value.Trim();

        if (await DelayedAsync(Attempt(source, entered), cancellationToken).ConfigureAwait(false) is Error delayed)
        {
            return (Result.Failure(delayed), false);
        }

        if (Destination(kind, entered) is not (string canonical, SendDestination destination))
        {
            return (Result.Failure(Error.From(ErrorCodes.IdentifierInvalid)), false);
        }

        live.Stage(kind, canonical);

        // AUTH-ABUSE-004: a send the restrictions refuse leaves nothing staged and no
        // code behind it.
        if (await SendCodeAsync(live, kind, destination, source, cancellationToken).ConfigureAwait(false)
            is Error refused)
        {
            return (Result.Failure(refused), false);
        }

        await sessions.RecordAsync(live, cancellationToken).ConfigureAwait(false);

        return (Result.Success(), true);
    }

    private async ValueTask<(Result Answer, bool Commits)> VerifiedAsync(
        CollectionSession? live,
        IdentifierKind kind,
        [NeverLogged] string code,
        string source,
        CancellationToken cancellationToken)
    {
        if (live is null)
        {
            return (Gone(), false);
        }

        // AUTH-ABUSE-001: every code is first held to the delay of the source that
        // presents it, before the contact it names is looked for.
        var presenting = new ThrottleAttempt(source, Identifier: null);

        if (await DelayedAsync(presenting, cancellationToken).ConfigureAwait(false) is Error held)
        {
            return (Result.Failure(held), false);
        }

        // Chapter 09 section 7a: a kind the session holds no contact of names nothing
        // to count against but the source, and is answered as a lapsed code.
        if (live.Contact(kind) is not CollectedContact contact)
        {
            return await CountedAsync(presenting, Error.From(ErrorCodes.CodeExpired), cancellationToken)
                .ConfigureAwait(false);
        }

        ThrottleAttempt attempt = Attempt(source, contact.Value);

        if (await DelayedAsync(attempt, cancellationToken).ConfigureAwait(false) is Error delayed)
        {
            return (Result.Failure(delayed), false);
        }

        byte[] holder = Holder(live.Id, kind);

        // AUTH-FACT-004: the try is read, compared and counted on the code's record
        // under its lock. A wrong code is counted against the source and the value; one
        // past its life or its attempts against the source alone.
        if ((await codes.PresentAsync(holder, code, cancellationToken).ConfigureAwait(false))
            .Match(() => (Error?)null, error => error) is Error refused)
        {
            return refused.Code == ErrorCodes.CodeInvalid
                ? await CountedAsync(attempt, refused, cancellationToken).ConfigureAwait(false)
                : refused.Code == ErrorCodes.CodeExpired
                    ? await CountedAsync(presenting, refused, cancellationToken).ConfigureAwait(false)
                    : (Result.Failure(refused), false);
        }

        live.Verify(kind, time.GetUtcNow());

        await sessions.RecordAsync(live, cancellationToken).ConfigureAwait(false);

        // AUTH-ABUSE-004: the code was presented right, which releases what the send
        // that carried it counted once.
        await restrictions
            .AnsweredAsync(SendReferences.Naming(holder, code), cancellationToken)
            .ConfigureAwait(false);

        return (Result.Success(), true);
    }

    private async ValueTask<Error?> SendCodeAsync(
        CollectionSession session,
        IdentifierKind kind,
        SendDestination destination,
        string source,
        CancellationToken cancellationToken)
    {
        Error? failure = null;

        IReadOnlyList<string> languages = (await configuration
                .ReadAsync(Settings.NotificationLanguages, cancellationToken).ConfigureAwait(false))
            .Match(value => value, error => Held<IReadOnlyList<string>>(error, ref failure));

        if (failure is not null)
        {
            return failure;
        }

        byte[] holder = Holder(session.Id, kind);

        // AUTH-ABUSE-004: the code is issued and its message undertaken in one unit of
        // work, so a send the restrictions refuse leaves no code behind it. A code
        // issued again replaces the one before it.
        string code = (await codes.IssueAsync(holder, cancellationToken).ConfigureAwait(false))
            .Match(value => value, error => Held<string>(error, ref failure));

        if (failure is not null)
        {
            return failure;
        }

        // REC-COL-001: the person types the code into the host's screen, so no link
        // goes with it.
        Result<SendReference> sent = await sending
            .UndertakeAsync(
                new OutboundMessage(
                    destination,
                    MessageKind.VerificationCode,
                    RestrictionPurpose.Verification,
                    source,
                    RecipientLanguage.Found(session.Language, languages))
                {
                    Values = new Dictionary<string, string>(capacity: 1, StringComparer.Ordinal)
                    {
                        ["code"] = code,
                    },
                },
                cancellationToken)
            .ConfigureAwait(false);

        if (sent.Match(_ => (Error?)null, error => error) is Error refused)
        {
            return refused;
        }

        await restrictions
            .CarriesAsync(
                sent.Match(reference => reference, _ => default),
                SendReferences.Naming(holder, code),
                cancellationToken)
            .ConfigureAwait(false);

        return null;
    }

    // IDN-ACCT-004: a contact is canonicalised as an account's identifier is.
    private static (string Canonical, SendDestination Destination)? Destination(IdentifierKind kind, string entered)
    {
        if (kind is IdentifierKind.Email)
        {
            return EmailAddress.TryParse(entered, out EmailAddress address)
                ? (address.Value, SendDestination.Of(address))
                : null;
        }

        return PhoneNumber.TryParse(entered, out PhoneNumber number)
            ? (number.Value, SendDestination.Of(number))
            : null;
    }

    // AUTH-ABUSE-001: a session's asks and tries are held to the delay the source of
    // the request in hand and the value's keyed hash have earned.
    private ThrottleAttempt Attempt(string source, string value) =>
        new(source, throttle.Identify(value, usernames: false));

    private async ValueTask<Error?> DelayedAsync(ThrottleAttempt attempt, CancellationToken cancellationToken)
    {
        Error? failure = null;

        TimeSpan delay = (await throttle.DelayAsync(attempt, cancellationToken).ConfigureAwait(false))
            .Match(value => value, error => Held<TimeSpan>(error, ref failure));

        if (failure is not null)
        {
            return failure;
        }

        return delay > TimeSpan.Zero ? Error.Throttled(time.GetUtcNow() + delay) : null;
    }

    // AUTH-ABUSE-001, CONV-DESIGN-003: the count stands whatever the outcome, so the
    // refusal that made it commits.
    private async ValueTask<(Result Answer, bool Commits)> CountedAsync(
        ThrottleAttempt attempt,
        Error refusal,
        CancellationToken cancellationToken) =>
        (await throttle.FailedAsync(attempt, cancellationToken).ConfigureAwait(false))
            .Match(
                () => (Result.Failure(refusal), true),
                error => (Result.Failure(error), false));

    // D-166 X3: a step decided on the session's one document is decided on its row
    // under the lock, from the read to the write. CONV-DESIGN-003: the answer says
    // whether it commits; a step whose refusal keeps a write is decided in the
    // outermost unit of work, so what the refusal keeps commits whatever a caller does
    // after it.
    private async ValueTask<Result> HeldAsync(
        CollectionSessionId id,
        Func<CollectionSession?, ValueTask<(Result Answer, bool Commits)>> decide,
        bool keeps,
        CancellationToken cancellationToken)
    {
        Result begun = keeps
            ? await work.BeginOutermostAsync(cancellationToken).ConfigureAwait(false)
            : (await work.BeginAsync(cancellationToken).ConfigureAwait(false)).Match(_ => Result.Success(), Result.Failure);

        if (begun.Match<Error?>(() => null, error => error) is Error notBegun)
        {
            return Result.Failure(notBegun);
        }

        (Result decided, bool commits) = await decide(await HoldAsync(id, cancellationToken).ConfigureAwait(false))
            .ConfigureAwait(false);

        if (!commits)
        {
            await work.RollbackAsync().ConfigureAwait(false);

            return decided;
        }

        return (await work.CommitAsync(cancellationToken).ConfigureAwait(false))
            .Match(() => decided, Result.Failure);
    }

    private async ValueTask<CollectionSession?> LiveAsync(
        CollectionSessionId id,
        CancellationToken cancellationToken)
    {
        CollectionSession? session = await sessions.FindAsync(id, cancellationToken).ConfigureAwait(false);

        return session is null || session.HasExpired(time.GetUtcNow()) ? null : session;
    }

    private static Result Gone() => Result.Failure(Error.From(ErrorCodes.SessionExpired));

    private static Result OutOfOrder() => Result.Failure(Error.From(ErrorCodes.CollectionIncomplete));

    private static TValue Held<TValue>(Error error, ref Error? failure)
    {
        failure = error;

        return default!;
    }
}
