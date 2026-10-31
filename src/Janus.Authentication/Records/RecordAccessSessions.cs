using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Factors;
using Janus.Authentication.Organizations;
using Janus.Authentication.Sending;
using Janus.Core;
using Janus.Core.Configuration;

namespace Janus.Authentication.Records;

/// <summary>
/// The record access session's steps, which its endpoints map: the ask of a code, its
/// opening by that code, its state, a reference, and its end (REC-ACC-001).
/// </summary>
/// <param name="sessions">Where the sessions are held.</param>
/// <param name="records">The person records a session reaches.</param>
/// <param name="audit">Where each record reached is recorded.</param>
/// <param name="organizations">The organizations, whose names the state shows.</param>
/// <param name="codes">The codes a session is opened by.</param>
/// <param name="sending">The governed send a code goes out through.</param>
/// <param name="restrictions">What releases a send's count once its code is answered.</param>
/// <param name="nonExistence">The answer to an ask no code answers.</param>
/// <param name="signals">What the carrier reports of a number before a code is texted to it.</param>
/// <param name="throttle">The delays of AUTH-ABUSE-001.</param>
/// <param name="configuration">The deployment's settings.</param>
/// <param name="work">The unit of work every step writes through.</param>
/// <param name="time">The deployment's clock.</param>
/// <remarks>
/// Implements REC-ACC-001, AUTH-FACT-002b, AUTH-FACT-004, AUTH-ABUSE-001 and
/// AUTH-ABUSE-003. The session is the browser's, as a collection session is, so its steps
/// are no operation of the contract and take no access context (chapter 07, D-221). An
/// ask is answered the same whatever is held, and so is the code it leaves outstanding:
/// where nothing is sent, a record that no code answers stands in its place, so a wrong
/// code is refused alike either way.
/// </remarks>
internal sealed class RecordAccessSessions(
    IRecordAccessSessionStore sessions,
    IRecordDirectory records,
    IRecordAudit audit,
    IOrganizationDirectory organizations,
    VerificationCodes codes,
    IGovernedSend sending,
    ISendingRestrictions restrictions,
    NonExistenceNotice nonExistence,
    PhoneSignals signals,
    ThrottleService throttle,
    IConfigurationStore configuration,
    IUnitOfWork work,
    TimeProvider time)
{
    // A record access code by text is a phone code, which is what the carrier's signal
    // is asked about (AUTH-FACT-002b).
    private static readonly Factor TextedCode =
        FactorCatalogue.Sent[(IdentifierKind.Phone, MessageChannels.Factors[MessageKind.RecordAccessCode])];

    /// <summary>
    /// Asks for a code to an email or phone, replacing the session the browser carried.
    /// </summary>
    /// <param name="signedIn">Who is signed in on the browser, or nothing.</param>
    /// <param name="carried">The record access session the browser carries, or nothing.</param>
    /// <param name="kind">An email or a phone.</param>
    /// <param name="value">The value entered.</param>
    /// <param name="language">The language the browser asked in.</param>
    /// <param name="source">The address the request came from.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// The session asked for, the same whatever is held; or
    /// <c>identity.record.signedin</c>, <c>auth.throttled</c>,
    /// <c>identity.identifier.invalid</c>, or the governed send's refusal, nothing asked.
    /// </returns>
    /// <exception cref="ArgumentNullException">A part is absent.</exception>
    public async ValueTask<Result<RecordAccessSession>> AskAsync(
        AccessContext? signedIn,
        RecordAccessSessionId? carried,
        IdentifierKind kind,
        string value,
        string language,
        string source,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(language);
        ArgumentNullException.ThrowIfNull(source);

        // REC-ACC-001: a person who holds an account reaches their records through it.
        if (signedIn is not null)
        {
            return Result.Failure<RecordAccessSession>(Error.From(ErrorCodes.RecordSignedIn));
        }

        string entered = value.Trim();
        byte[] identified = throttle.Identify(entered, usernames: false);
        var attempt = new ThrottleAttempt(source, identified);

        if (await DelayedAsync(attempt, cancellationToken).ConfigureAwait(false) is Error delayed)
        {
            return Result.Failure<RecordAccessSession>(delayed);
        }

        if (Destination(kind, entered) is not (string canonical, SendDestination destination))
        {
            return Result.Failure<RecordAccessSession>(Error.From(ErrorCodes.IdentifierInvalid));
        }

        Error? failure = null;

        TimeSpan codeLifetime = (await configuration
                .ReadAsync(Settings.CodeSigninLifetime, cancellationToken).ConfigureAwait(false))
            .Match(found => found, error => Held<TimeSpan>(error, ref failure));

        IReadOnlyList<string> languages = (await configuration
                .ReadAsync(Settings.NotificationLanguages, cancellationToken).ConfigureAwait(false))
            .Match(found => found, error => Held<IReadOnlyList<string>>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure<RecordAccessSession>(failure);
        }

        IReadOnlyList<byte[]> fingerprints = records.FingerprintsOf(canonical);
        bool held = await records.ReachableHoldingAsync(kind, fingerprints, cancellationToken).ConfigureAwait(false);

        // AUTH-FACT-002b: no code goes to a number the carrier reports a recent change of
        // SIM or of network for. The question is asked of every number, held or not, so
        // nothing about what is held is told either way (AUTH-ABUSE-003 AC1).
        bool allowed = kind is not IdentifierKind.Phone
            || await signals.AllowsAsync(TextedCode, canonical, subject: null, cancellationToken).ConfigureAwait(false);

        var session = RecordAccessSession.Ask(
            RecordAccessSessionId.New(time),
            kind,
            fingerprints,
            identified,
            time.GetUtcNow(),
            codeLifetime);

        Error? refused = held && allowed
            ? await SentAsync(session, carried, destination, RecipientLanguage.Found(language, languages), source, cancellationToken)
                .ConfigureAwait(false)
            : await WithheldAsync(session, carried, destination, language, source, unheld: !held, cancellationToken)
                .ConfigureAwait(false);

        return refused is null ? Result.Success(session) : Result.Failure<RecordAccessSession>(refused);
    }

    /// <summary>
    /// Opens the session the browser asked for by its code, reaching the records that
    /// hold the value as a contact verified at a collection within
    /// <c>records.proof.age</c>.
    /// </summary>
    /// <param name="carried">The record access session the browser carries, or nothing.</param>
    /// <param name="code">The code entered.</param>
    /// <param name="source">The address the request came from.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// Success, or <c>auth.throttled</c>, <c>auth.code.invalid</c>, or
    /// <c>auth.code.expired</c> for a code past its life or its attempts and where no code
    /// was asked for in the browser.
    /// </returns>
    /// <exception cref="ArgumentNullException">A part is absent.</exception>
    public async ValueTask<Result> VerifyAsync(
        RecordAccessSessionId? carried,
        [NeverLogged] string code,
        string source,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(code);
        ArgumentNullException.ThrowIfNull(source);

        // AUTH-ABUSE-001: every code is first held to the delay of the source that
        // presents it, before the session it names is looked for.
        var presenting = new ThrottleAttempt(source, Identifier: null);

        if (await DelayedAsync(presenting, cancellationToken).ConfigureAwait(false) is Error delayed)
        {
            return Result.Failure(delayed);
        }

        return await HeldAsync(
                carried,
                live => OpenedAsync(live, presenting, code, cancellationToken),
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// What an open session shows: the records it reaches that are still in its reach.
    /// </summary>
    /// <param name="carried">The record access session the browser carries, or nothing.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The state, or <c>auth.session.expired</c>.</returns>
    public async ValueTask<Result<RecordAccessState>> StateAsync(
        RecordAccessSessionId? carried,
        CancellationToken cancellationToken)
    {
        if (carried is not RecordAccessSessionId named
            || await sessions.FindAsync(named, cancellationToken).ConfigureAwait(false) is not RecordAccessSession found
            || !Open(found))
        {
            return Result.Failure<RecordAccessState>(Error.From(ErrorCodes.SessionExpired));
        }

        IReadOnlyList<ReachedRecord> reached = await records
            .InReachAsync(found.Reached, found.Kind, found.Fingerprints, cancellationToken)
            .ConfigureAwait(false);
        var shown = new List<AccessibleRecord>(reached.Count);

        foreach (ReachedRecord record in reached)
        {
            OrganizationStanding? organization = await organizations
                .FindAsync(record.Organization, cancellationToken)
                .ConfigureAwait(false);

            shown.Add(new AccessibleRecord(
                record.Subject,
                record.Organization,
                organization?.Name ?? string.Empty,
                record.CreatedAt,
                record.State));
        }

        return Result.Success(new RecordAccessState(found.ExpiresAt, shown));
    }

    /// <summary>
    /// Whether an open session reaches one record, which is what every right exercised
    /// on the record from the session asks first (REC-ACC-002).
    /// </summary>
    /// <param name="carried">The record access session the browser carries, or nothing.</param>
    /// <param name="record">The record's subject.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// The record as the session reaches it, or <c>auth.session.expired</c> where no
    /// session is open, <c>identity.record.notfound</c> where the session does not reach
    /// it, or reaches it no longer.
    /// </returns>
    public async ValueTask<Result<ReachedRecord>> ReachesAsync(
        RecordAccessSessionId? carried,
        SubjectId record,
        CancellationToken cancellationToken)
    {
        if (carried is not RecordAccessSessionId named
            || await sessions.FindAsync(named, cancellationToken).ConfigureAwait(false) is not RecordAccessSession found
            || !Open(found))
        {
            return Result.Failure<ReachedRecord>(Error.From(ErrorCodes.SessionExpired));
        }

        // Chapter 09 section 7a: a record the session never reached and one it reaches no
        // longer are answered alike, so the answer says nothing of what else is held.
        if (!found.Reached.Contains(record))
        {
            return Result.Failure<ReachedRecord>(Error.From(ErrorCodes.RecordNotFound));
        }

        return (await records
                .InReachAsync([record], found.Kind, found.Fingerprints, cancellationToken)
                .ConfigureAwait(false)) is [ReachedRecord reached]
            ? Result.Success(reached)
            : Result.Failure<ReachedRecord>(Error.From(ErrorCodes.RecordNotFound));
    }

    /// <summary>
    /// Adds every reachable record holding the session's value and a reference, whatever
    /// the age of the value's verification.
    /// </summary>
    /// <param name="carried">The record access session the browser carries, or nothing.</param>
    /// <param name="reference">The reference given, trimmed and inside its bounds.</param>
    /// <param name="source">The address the request came from.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// Success whether or not a record was added, or <c>auth.session.expired</c>,
    /// <c>auth.throttled</c>.
    /// </returns>
    /// <exception cref="ArgumentNullException">A part is absent.</exception>
    public ValueTask<Result> ReferenceAsync(
        RecordAccessSessionId? carried,
        string reference,
        string source,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(reference);
        ArgumentNullException.ThrowIfNull(source);

        return HeldAsync(
            carried,
            live => ReferencedAsync(live, CanonicalForm.Of(reference), source, cancellationToken),
            cancellationToken);
    }

    /// <summary>
    /// Ends the session the browser carries and the code it had outstanding.
    /// </summary>
    /// <param name="carried">The record access session the browser carries, or nothing.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>Success, whether or not a session was there to end.</returns>
    public async ValueTask<Result> EndAsync(
        RecordAccessSessionId? carried,
        CancellationToken cancellationToken)
    {
        if (carried is not RecordAccessSessionId named)
        {
            return Result.Success();
        }

        if ((await work.BeginAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(_ => null, error => error) is Error notBegun)
        {
            return Result.Failure(notBegun);
        }

        await RemovedAsync(named, cancellationToken).ConfigureAwait(false);

        return await work.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    // What the session's code is held under. It is a digest of eighteen bytes, so it is
    // never the holder of a collection's contact, one of seventeen, nor of a
    // registration's identifier, one of thirty-two.
    private static byte[] Holder(RecordAccessSessionId session)
    {
        Span<byte> named = stackalloc byte[18];

        _ = session.Value.TryWriteBytes(named);

        return SHA256.HashData(named);
    }

    // IDN-ACCT-004: a value is canonicalised as an account's identifier is; a kind that
    // is neither an email nor a phone names nowhere a code could go.
    private static (string Canonical, SendDestination Destination)? Destination(IdentifierKind kind, string entered)
    {
        if (kind is IdentifierKind.Email)
        {
            return EmailAddress.TryParse(entered, out EmailAddress address)
                ? (address.Value, SendDestination.Of(address))
                : null;
        }

        return kind is IdentifierKind.Phone && PhoneNumber.TryParse(entered, out PhoneNumber number)
            ? (number.Value, SendDestination.Of(number))
            : null;
    }

    private static TValue Held<TValue>(Error error, ref Error? failure)
    {
        failure = error;

        return default!;
    }

    private bool Open(RecordAccessSession session) => session.IsOpen && !session.HasExpired(time.GetUtcNow());

    // REC-ACC-001: a code goes to the value, and the session it opens is bound to the
    // browser that asked. AUTH-ABUSE-004: the code is issued and its message undertaken
    // in one unit of work, so a send the restrictions refuse leaves no session or code.
    private async ValueTask<Error?> SentAsync(
        RecordAccessSession session,
        RecordAccessSessionId? carried,
        SendDestination destination,
        string? language,
        string source,
        CancellationToken cancellationToken)
    {
        if (await BegunAsync(session.Kind, cancellationToken).ConfigureAwait(false) is Error notBegun)
        {
            return notBegun;
        }

        await AddedAsync(session, carried, cancellationToken).ConfigureAwait(false);

        byte[] holder = Holder(session.Id);
        Error? failure = null;

        string code = (await codes.IssueAsync(holder, Settings.CodeSigninLifetime, cancellationToken).ConfigureAwait(false))
            .Match(issued => issued, error => Held<string>(error, ref failure));

        if (failure is null)
        {
            Result<SendReference> sent = await sending
                .UndertakeAsync(
                    new OutboundMessage(destination, MessageKind.RecordAccessCode, RestrictionPurpose.SignIn, source, language)
                    {
                        Values = new Dictionary<string, string>(capacity: 1, StringComparer.Ordinal)
                        {
                            ["code"] = code,
                        },
                    },
                    cancellationToken)
                .ConfigureAwait(false);

            failure = sent.Match(_ => (Error?)null, error => error);

            if (failure is null)
            {
                await restrictions
                    .CarriesAsync(
                        sent.Match(reference => reference, _ => default),
                        SendReferences.Naming(holder, code),
                        cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        if (failure is not null)
        {
            await work.RollbackAsync().ConfigureAwait(false);

            return failure;
        }

        return (await work.CommitAsync(cancellationToken).ConfigureAwait(false)).Match<Error?>(() => null, error => error);
    }

    // AUTH-ABUSE-003: an ask no code answers, because no reachable record holds the value
    // or the carrier's signal withholds it, is answered as the other and counted as the
    // code would have been. The browser still carries a session, whose code no try
    // answers, so a code presented is refused as one presented after a send.
    private async ValueTask<Error?> WithheldAsync(
        RecordAccessSession session,
        RecordAccessSessionId? carried,
        SendDestination destination,
        string language,
        string source,
        bool unheld,
        CancellationToken cancellationToken)
    {
        if ((await nonExistence
                .AnswerAsync(
                    destination,
                    MessageKind.RecordAccessCode,
                    RestrictionPurpose.SignIn,
                    source,
                    language,
                    unheld,
                    outermost: session.Kind is IdentifierKind.Phone,
                    cancellationToken)
                .ConfigureAwait(false))
            .Match<Error?>(() => null, error => error) is Error refused)
        {
            return refused;
        }

        return await UnsentAsync(session, carried, cancellationToken).ConfigureAwait(false);
    }

    // The session asked where no code went out, whose code no try answers.
    private async ValueTask<Error?> UnsentAsync(
        RecordAccessSession session,
        RecordAccessSessionId? carried,
        CancellationToken cancellationToken)
    {
        if ((await work.BeginAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(_ => null, error => error) is Error notBegun)
        {
            return notBegun;
        }

        await AddedAsync(session, carried, cancellationToken).ConfigureAwait(false);

        if ((await codes.WithholdAsync(Holder(session.Id), Settings.CodeSigninLifetime, cancellationToken)
                .ConfigureAwait(false))
            .Match<Error?>(() => null, error => error) is Error unheldCode)
        {
            await work.RollbackAsync().ConfigureAwait(false);

            return unheldCode;
        }

        return (await work.CommitAsync(cancellationToken).ConfigureAwait(false)).Match<Error?>(() => null, error => error);
    }

    // An ask whose code may go by text asks the carrier's signal first, whose answer is
    // recorded by the send, so its unit of work is the outermost (CONV-DESIGN-003, D-207).
    private async ValueTask<Error?> BegunAsync(IdentifierKind kind, CancellationToken cancellationToken)
    {
        Result begun = kind is IdentifierKind.Phone
            ? await work.BeginOutermostAsync(cancellationToken).ConfigureAwait(false)
            : (await work.BeginAsync(cancellationToken).ConfigureAwait(false)).Match(_ => Result.Success(), Result.Failure);

        return begun.Match<Error?>(() => null, error => error);
    }

    // A new ask replaces the session the browser carried, and the code it had outstanding.
    private async ValueTask AddedAsync(
        RecordAccessSession session,
        RecordAccessSessionId? carried,
        CancellationToken cancellationToken)
    {
        if (carried is RecordAccessSessionId replaced)
        {
            await RemovedAsync(replaced, cancellationToken).ConfigureAwait(false);
        }

        await sessions.AddAsync(session, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask RemovedAsync(RecordAccessSessionId id, CancellationToken cancellationToken)
    {
        if (await sessions.FindForUpdateAsync(id, cancellationToken).ConfigureAwait(false) is RecordAccessSession held)
        {
            await codes.EndAsync(Holder(held.Id), cancellationToken).ConfigureAwait(false);
            await sessions.RemoveAsync(held.Id, cancellationToken).ConfigureAwait(false);
        }
    }

    private async ValueTask<(Result Answer, bool Commits)> OpenedAsync(
        RecordAccessSession? live,
        ThrottleAttempt presenting,
        [NeverLogged] string code,
        CancellationToken cancellationToken)
    {
        // Chapter 09 section 7a: a browser that asked for no code, or whose code has
        // already opened its session, names nothing to count against but the source, and
        // is answered as a lapsed code.
        if (live is null || live.IsOpen)
        {
            return await CountedAsync(presenting, Error.From(ErrorCodes.CodeExpired), cancellationToken)
                .ConfigureAwait(false);
        }

        var attempt = new ThrottleAttempt(presenting.Source, live.Throttled);

        if (await DelayedAsync(attempt, cancellationToken).ConfigureAwait(false) is Error delayed)
        {
            return (Result.Failure(delayed), false);
        }

        byte[] holder = Holder(live.Id);

        // AUTH-FACT-004: the code is an authentication code. A wrong one is counted
        // against the source and the value; one past its life or its attempts against
        // the source alone.
        if ((await codes.PresentAsync(holder, code, Settings.CodeSigninAttempts, cancellationToken).ConfigureAwait(false))
            .Match(() => (Error?)null, error => error) is Error refused)
        {
            return refused.Code == ErrorCodes.CodeInvalid
                ? await CountedAsync(attempt, refused, cancellationToken).ConfigureAwait(false)
                : refused.Code == ErrorCodes.CodeExpired
                    ? await CountedAsync(presenting, refused, cancellationToken).ConfigureAwait(false)
                    : (Result.Failure(refused), false);
        }

        Error? failure = null;

        TimeSpan lifetime = (await configuration
                .ReadAsync(Settings.RecordsAccessLifetime, cancellationToken).ConfigureAwait(false))
            .Match(found => found, error => Held<TimeSpan>(error, ref failure));

        TimeSpan proofAge = (await configuration
                .ReadAsync(Settings.RecordsProofAge, cancellationToken).ConfigureAwait(false))
            .Match(found => found, error => Held<TimeSpan>(error, ref failure));

        if (failure is not null)
        {
            return (Result.Failure(failure), false);
        }

        DateTimeOffset now = time.GetUtcNow();

        live.Open(now, lifetime);

        IReadOnlyList<ReachedRecord> reached = await records
            .ReachedAsync(live.Kind, live.Fingerprints, now - proofAge, cancellationToken)
            .ConfigureAwait(false);

        await ReachAsync(live, reached, byReference: false, now, cancellationToken).ConfigureAwait(false);

        // AUTH-ABUSE-004: the code was presented right, which releases what the send that
        // carried it counted once.
        await restrictions
            .AnsweredAsync(SendReferences.Naming(holder, code), cancellationToken)
            .ConfigureAwait(false);

        return (Result.Success(), true);
    }

    private async ValueTask<(Result Answer, bool Commits)> ReferencedAsync(
        RecordAccessSession? live,
        string reference,
        string source,
        CancellationToken cancellationToken)
    {
        if (live is null || !live.IsOpen)
        {
            return (Result.Failure(Error.From(ErrorCodes.SessionExpired)), false);
        }

        var attempt = new ThrottleAttempt(source, live.Throttled);

        if (await DelayedAsync(attempt, cancellationToken).ConfigureAwait(false) is Error delayed)
        {
            return (Result.Failure(delayed), false);
        }

        IReadOnlyList<ReachedRecord> referenced = await records
            .ReferencedAsync(live.Kind, live.Fingerprints, reference, cancellationToken)
            .ConfigureAwait(false);

        // REC-ACC-001: a reference that adds none is counted as a wrong code is, and is
        // answered as one that adds some.
        if (!await ReachAsync(live, referenced, byReference: true, time.GetUtcNow(), cancellationToken)
            .ConfigureAwait(false))
        {
            (Result counted, bool commits) = await CountedAsync(attempt, Error.From(ErrorCodes.CodeInvalid), cancellationToken)
                .ConfigureAwait(false);

            return commits ? (Result.Success(), true) : (counted, false);
        }

        return (Result.Success(), true);
    }

    // REC-ACC-001: each record the session now reaches is recorded as reached, the record
    // acting, filed under its organization.
    private async ValueTask<bool> ReachAsync(
        RecordAccessSession live,
        IReadOnlyList<ReachedRecord> reached,
        bool byReference,
        DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        var added = new HashSet<SubjectId>(live.Reach(reached.Select(record => record.Subject)));

        foreach (ReachedRecord record in reached)
        {
            if (added.Contains(record.Subject))
            {
                await audit
                    .AccessedAsync(record.Subject, record.Organization, live.Id, byReference, at, cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        await sessions.RecordAsync(live, cancellationToken).ConfigureAwait(false);

        return added.Count > 0;
    }

    private async ValueTask<Error?> DelayedAsync(ThrottleAttempt attempt, CancellationToken cancellationToken)
    {
        Error? failure = null;

        TimeSpan delay = (await throttle.DelayAsync(attempt, cancellationToken).ConfigureAwait(false))
            .Match(found => found, error => Held<TimeSpan>(error, ref failure));

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

    // D-166 X3: a step is decided on the session's row under its lock, from the read to
    // the write. CONV-DESIGN-003: a step whose refusal keeps a write is decided in the
    // outermost unit of work, so what the refusal keeps commits whatever a caller does
    // after it.
    private async ValueTask<Result> HeldAsync(
        RecordAccessSessionId? carried,
        Func<RecordAccessSession?, ValueTask<(Result Answer, bool Commits)>> decide,
        CancellationToken cancellationToken)
    {
        if ((await work.BeginOutermostAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(() => null, error => error) is Error notBegun)
        {
            return Result.Failure(notBegun);
        }

        RecordAccessSession? held = carried is RecordAccessSessionId named
            ? await sessions.FindForUpdateAsync(named, cancellationToken).ConfigureAwait(false)
            : null;

        (Result decided, bool commits) = await decide(
                held is null || held.HasExpired(time.GetUtcNow()) ? null : held)
            .ConfigureAwait(false);

        if (!commits)
        {
            await work.RollbackAsync().ConfigureAwait(false);

            return decided;
        }

        return (await work.CommitAsync(cancellationToken).ConfigureAwait(false))
            .Match(() => decided, Result.Failure);
    }
}
