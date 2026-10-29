using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Accounts;
using Janus.Authentication.Factors;
using Janus.Authentication.Identifiers;
using Janus.Authentication.Organizations;
using Janus.Authentication.Sending;
using Janus.Core;
using Janus.Core.Configuration;

namespace Janus.Authentication.Records;

/// <summary>
/// What an account does to attach the person records a proof offers it: list those it
/// holds, read the offer, prove an email or phone by a code to find them, and attach.
/// </summary>
/// <param name="offers">Where each session's offer is held.</param>
/// <param name="offering">What makes the offer a proof brings.</param>
/// <param name="records">The person records attached.</param>
/// <param name="audit">Where each attachment is recorded.</param>
/// <param name="organizations">The organizations, whose names the list shows.</param>
/// <param name="identifiers">The account's identifiers, which a code to find its records goes to.</param>
/// <param name="accounts">The account, whose state an attachment is decided on under its lock.</param>
/// <param name="restriction">What refuses a restricted account's attachment.</param>
/// <param name="codes">The codes a find is proved by.</param>
/// <param name="sending">The governed send a code goes out through.</param>
/// <param name="restrictions">What releases a send's count once its code is answered.</param>
/// <param name="signals">What the carrier reports of a number before a code is texted to it.</param>
/// <param name="throttle">The delays of AUTH-ABUSE-001.</param>
/// <param name="events">Where each attachment is announced.</param>
/// <param name="configuration">The deployment's settings.</param>
/// <param name="work">The unit of work every step writes through.</param>
/// <param name="time">The deployment's clock.</param>
/// <remarks>
/// Implements REC-ATT-001, AUTHZ-GATE-006, AUTH-FACT-002b, AUTH-FACT-004 and
/// AUTH-ABUSE-001. An attachment sets the record's account and instant and nothing else:
/// the record keeps its subject, key, consents and organization (IDN-ACCT-003).
/// </remarks>
internal sealed class RecordAttachments(
    IRecordOfferStore offers,
    RecordOffers offering,
    IRecordDirectory records,
    IRecordAudit audit,
    IOrganizationDirectory organizations,
    IIdentifierDirectory identifiers,
    IAccountDirectory accounts,
    ISettingsRestriction restriction,
    VerificationCodes codes,
    IGovernedSend sending,
    ISendingRestrictions restrictions,
    PhoneSignals signals,
    ThrottleService throttle,
    IEvents events,
    IConfigurationStore configuration,
    IUnitOfWork work,
    TimeProvider time)
{
    // A find code by text is a phone code with no link, which is what the carrier's
    // signal is asked about (AUTH-FACT-002b).
    private static readonly Factor TextedCode = FactorCatalogue.Sent[(IdentifierKind.Phone, false)];

    /// <summary>
    /// The records attached to the asking account.
    /// </summary>
    /// <param name="context">The account asking.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The records, or <c>authz.denied</c>.</returns>
    /// <exception cref="ArgumentNullException">The context is absent.</exception>
    public async ValueTask<Result<IReadOnlyList<AttachedRecord>>> ListAttachedAsync(
        AccessContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Effective is not SubjectId account)
        {
            return Result.Failure<IReadOnlyList<AttachedRecord>>(Error.From(ErrorCodes.Denied));
        }

        IReadOnlyList<Attachment> attached = await records
            .AttachedToAsync(account, cancellationToken)
            .ConfigureAwait(false);
        var listed = new List<AttachedRecord>(attached.Count);

        foreach (Attachment record in attached)
        {
            OrganizationStanding? organization = await organizations
                .FindAsync(record.Organization, cancellationToken)
                .ConfigureAwait(false);

            listed.Add(new AttachedRecord(
                record.Record,
                record.Organization,
                organization?.Name ?? string.Empty,
                record.CreatedAt,
                record.AttachedAt));
        }

        return Result.Success<IReadOnlyList<AttachedRecord>>(listed);
    }

    /// <summary>
    /// The record attached to the account asking, as its rights reach it.
    /// </summary>
    /// <param name="context">The account asking.</param>
    /// <param name="record">The record's subject.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// The record, or <c>identity.record.notfound</c> for one not attached to the account,
    /// <c>authz.denied</c>.
    /// </returns>
    /// <exception cref="ArgumentNullException">The context is absent.</exception>
    public async ValueTask<Result<ReachedRecord>> ReachesAsync(
        AccessContext context,
        SubjectId record,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Effective is not SubjectId account)
        {
            return Result.Failure<ReachedRecord>(Error.From(ErrorCodes.Denied));
        }

        // REC-ATT-002, chapter 09 section 7a: a record attached to another account and one
        // no record bears are answered alike.
        return (await records.AttachedToAsync(account, cancellationToken).ConfigureAwait(false))
            .SingleOrDefault(attached => attached.Record == record) is Attachment found
            ? Result.Success(new ReachedRecord(found.Record, found.Organization, found.CreatedAt, found.State))
            : Result.Failure<ReachedRecord>(Error.From(ErrorCodes.RecordNotFound));
    }

    /// <summary>
    /// The offer the session holds, as a count and its expiry.
    /// </summary>
    /// <param name="context">The account asking.</param>
    /// <param name="session">The session the request arrived on.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The offer, none where none stands, or <c>authz.denied</c>.</returns>
    /// <exception cref="ArgumentNullException">The context is absent.</exception>
    public async ValueTask<Result<RecordOffer>> ReadOfferAsync(
        AccessContext context,
        SessionId session,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Effective is null)
        {
            return Result.Failure<RecordOffer>(Error.From(ErrorCodes.Denied));
        }

        return Result.Success(await ShownAsync(session, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>
    /// Sends a code to one of the account's verified emails or phones.
    /// </summary>
    /// <param name="context">The account asking.</param>
    /// <param name="session">The session the request arrived on.</param>
    /// <param name="identifier">The identifier the code goes to.</param>
    /// <param name="source">The address the request came from.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// Success, or <c>authz.denied</c>, <c>identity.identifier.invalid</c>,
    /// <c>auth.throttled</c>, or the governed send's refusal.
    /// </returns>
    /// <exception cref="ArgumentNullException">The context or the source is absent.</exception>
    public async ValueTask<Result> SendFindCodeAsync(
        AccessContext context,
        SessionId session,
        IdentifierId identifier,
        string source,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(source);

        if (context.Effective is not SubjectId account)
        {
            return Result.Failure(Error.From(ErrorCodes.Denied));
        }

        IReadOnlyList<HeldIdentifier> provable = await ProvableAsync(account, cancellationToken).ConfigureAwait(false);

        if (provable.FirstOrDefault(held => held.Id == identifier) is not HeldIdentifier target)
        {
            return Result.Failure(Error.From(ErrorCodes.IdentifierInvalid));
        }

        // AUTH-ABUSE-001: every ask is first held to the delay the source and the value
        // have earned.
        var attempt = new ThrottleAttempt(source, throttle.Identify(target.Canonical, usernames: false))
        {
            Account = account,
        };

        if (await DelayedAsync(attempt, cancellationToken).ConfigureAwait(false) is Error delayed)
        {
            return Result.Failure(delayed);
        }

        Error? failure = null;

        IReadOnlyList<string> languages = (await configuration
                .ReadAsync(Settings.NotificationLanguages, cancellationToken).ConfigureAwait(false))
            .Match(found => found, error => Held<IReadOnlyList<string>>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure(failure);
        }

        string? language = RecipientLanguage.Of(
            await identifiers.LanguageAsync(account, cancellationToken).ConfigureAwait(false),
            requested: null,
            languages);

        // REC-ATT-001, AUTH-FACT-002b: no code goes to a number the carrier reports a
        // recent change of SIM or of network for, and the ask is answered the same.
        bool allowed = target.Kind is not IdentifierKind.Phone
            || await signals.AllowsAsync(TextedCode, target.Canonical, account, cancellationToken).ConfigureAwait(false);

        // An ask whose code may go by text has asked the carrier's signal, whose answer
        // the send records, so its unit of work is the outermost (CONV-DESIGN-003, D-207).
        if (await BegunAsync(target.Kind, cancellationToken).ConfigureAwait(false) is Error notBegun)
        {
            return Result.Failure(notBegun);
        }

        // A new ask ends the code another of the account's values had outstanding on the
        // session, so the code presented proves the value the last ask named.
        foreach (HeldIdentifier other in provable.Where(held => held.Id != identifier))
        {
            await codes.EndAsync(Holder(session, other.Id), cancellationToken).ConfigureAwait(false);
        }

        byte[] holder = Holder(session, target.Id);

        failure = allowed
            ? await SentAsync(holder, account, target, language, source, cancellationToken).ConfigureAwait(false)
            : (await codes.WithholdAsync(holder, cancellationToken).ConfigureAwait(false))
                .Match(() => (Error?)null, error => error);

        if (failure is not null)
        {
            await work.RollbackAsync().ConfigureAwait(false);

            return Result.Failure(failure);
        }

        return await work.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Proves by its code the value the last ask on the session named, and makes the
    /// offer it brings.
    /// </summary>
    /// <param name="context">The account asking.</param>
    /// <param name="session">The session the request arrived on.</param>
    /// <param name="code">The code entered.</param>
    /// <param name="source">The address the request came from.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// The offer, or <c>authz.denied</c>, <c>auth.throttled</c>, <c>auth.code.invalid</c>,
    /// <c>auth.code.expired</c>.
    /// </returns>
    /// <exception cref="ArgumentNullException">A part is absent.</exception>
    public async ValueTask<Result<RecordOffer>> VerifyFindCodeAsync(
        AccessContext context,
        SessionId session,
        [NeverLogged] string code,
        string source,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(code);
        ArgumentNullException.ThrowIfNull(source);

        if (context.Effective is not SubjectId account)
        {
            return Result.Failure<RecordOffer>(Error.From(ErrorCodes.Denied));
        }

        // AUTH-ABUSE-001: every code is first held to the delay of the source that
        // presents it, before the value it names is looked for.
        var presenting = new ThrottleAttempt(source, Identifier: null);

        if (await DelayedAsync(presenting, cancellationToken).ConfigureAwait(false) is Error delayed)
        {
            return Result.Failure<RecordOffer>(delayed);
        }

        if ((await DecidedAsync(
                    () => ProvedAsync(session, account, presenting, code, cancellationToken),
                    cancellationToken)
                .ConfigureAwait(false))
            .Match<Error?>(() => null, error => error) is Error refused)
        {
            return Result.Failure<RecordOffer>(refused);
        }

        return Result.Success(await ShownAsync(session, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>
    /// Attaches each record the offer counted, and ends the offer.
    /// </summary>
    /// <param name="context">The account asking.</param>
    /// <param name="session">The session the request arrived on.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// How many were attached, or <c>authz.denied</c>, <c>authz.restricted</c>,
    /// <c>identity.account.stateconflict</c>.
    /// </returns>
    /// <exception cref="ArgumentNullException">The context is absent.</exception>
    public async ValueTask<Result<int>> AttachAsync(
        AccessContext context,
        SessionId session,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Effective is not SubjectId account)
        {
            return Result.Failure<int>(Error.From(ErrorCodes.Denied));
        }

        // AUTHZ-GATE-006: a restricted account attaches nothing.
        if (await restriction.RefusedAsync(context, cancellationToken).ConfigureAwait(false) is Error restricted)
        {
            return Result.Failure<int>(restricted);
        }

        if ((await work.BeginAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(_ => null, error => error) is Error notBegun)
        {
            return Result.Failure<int>(notBegun);
        }

        DateTimeOffset now = time.GetUtcNow();

        if (await offers.FindForUpdateAsync(session, cancellationToken).ConfigureAwait(false)
            is not OfferToAttach offer
            || offer.HasExpired(now))
        {
            await work.RollbackAsync().ConfigureAwait(false);

            return Result.Success(0);
        }

        Result<int> attached = await AttachedAsync(context, account, offer.Offered, now, cancellationToken)
            .ConfigureAwait(false);

        if (attached.Match<Error?>(_ => null, error => error) is Error unattached)
        {
            await work.RollbackAsync().ConfigureAwait(false);

            return Result.Failure<int>(unattached);
        }

        await offers.RemoveAsync(session, cancellationToken).ConfigureAwait(false);

        return (await work.CommitAsync(cancellationToken).ConfigureAwait(false))
            .Match(() => attached, Result.Failure<int>);
    }

    /// <summary>
    /// Attaches, inside the offer's life, each active record attached to no account that
    /// holds a value the offer proved and a reference.
    /// </summary>
    /// <param name="context">The account asking.</param>
    /// <param name="session">The session the request arrived on.</param>
    /// <param name="reference">The reference, trimmed and inside its bounds.</param>
    /// <param name="source">The address the request came from.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// How many were attached, or <c>authz.denied</c>, <c>authz.restricted</c>,
    /// <c>identity.record.nooffer</c>, <c>auth.throttled</c>,
    /// <c>identity.account.stateconflict</c>.
    /// </returns>
    /// <exception cref="ArgumentNullException">A part is absent.</exception>
    public async ValueTask<Result<int>> AttachByReferenceAsync(
        AccessContext context,
        SessionId session,
        string reference,
        string source,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(reference);
        ArgumentNullException.ThrowIfNull(source);

        if (context.Effective is not SubjectId account)
        {
            return Result.Failure<int>(Error.From(ErrorCodes.Denied));
        }

        if (await restriction.RefusedAsync(context, cancellationToken).ConfigureAwait(false) is Error restricted)
        {
            return Result.Failure<int>(restricted);
        }

        // REC-ATT-001: a reference is tried as a code is, against the source and the
        // account.
        var attempt = new ThrottleAttempt(source, Identifier: null) { Account = account };

        if (await DelayedAsync(attempt, cancellationToken).ConfigureAwait(false) is Error delayed)
        {
            return Result.Failure<int>(delayed);
        }

        if ((await work.BeginOutermostAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(() => null, error => error) is Error notBegun)
        {
            return Result.Failure<int>(notBegun);
        }

        DateTimeOffset now = time.GetUtcNow();

        if (await offers.FindForUpdateAsync(session, cancellationToken).ConfigureAwait(false)
            is not OfferToAttach offer
            || offer.HasExpired(now))
        {
            await work.RollbackAsync().ConfigureAwait(false);

            return Result.Failure<int>(Error.From(ErrorCodes.RecordNoOffer));
        }

        string folded = CanonicalForm.Of(reference);
        var referenced = new List<SubjectId>();

        foreach (ProvenValue value in offer.Proven)
        {
            referenced.AddRange(
                (await records
                    .ReferencedAsync(value.Kind, value.Fingerprints, folded, cancellationToken)
                    .ConfigureAwait(false))
                .Where(record => record.State is PersonRecordState.Active)
                .Select(record => record.Subject));
        }

        Result<int> attached = await AttachedAsync(
                context,
                account,
                [.. referenced.Distinct().OrderBy(subject => subject.Value)],
                now,
                cancellationToken)
            .ConfigureAwait(false);

        if (attached.Match<Error?>(_ => null, error => error) is Error unattached)
        {
            await work.RollbackAsync().ConfigureAwait(false);

            return Result.Failure<int>(unattached);
        }

        // REC-ATT-001: a reference that attaches none is counted as a wrong code is, and
        // the count commits.
        if (attached.Match(count => count, _ => 0) == 0
            && (await throttle.FailedAsync(attempt, cancellationToken).ConfigureAwait(false))
                .Match<Error?>(() => null, error => error) is Error uncounted)
        {
            await work.RollbackAsync().ConfigureAwait(false);

            return Result.Failure<int>(uncounted);
        }

        return (await work.CommitAsync(cancellationToken).ConfigureAwait(false))
            .Match(() => attached, Result.Failure<int>);
    }

    // What a find code is held under: the session and the identifier, so an ask on
    // another session leaves this one's code standing. It is a digest of thirty-three
    // bytes, so it is never the holder of a registration's identifier, one of
    // thirty-two, nor of a replace's confirmation, one of thirty-four.
    private static byte[] Holder(SessionId session, IdentifierId identifier)
    {
        Span<byte> named = stackalloc byte[33];

        _ = session.Value.TryWriteBytes(named);
        _ = identifier.Value.TryWriteBytes(named[16..]);
        named[32] = 1;

        return SHA256.HashData(named);
    }

    private static TValue Held<TValue>(Error error, ref Error? failure)
    {
        failure = error;

        return default!;
    }

    private static SendDestination Destination(HeldIdentifier identifier) =>
        identifier.Kind is IdentifierKind.Email
            ? SendDestination.Of(EmailAddress.TryParse(identifier.Canonical, out EmailAddress address)
                ? address
                : throw new InvalidOperationException("A held address is canonical already."))
            : SendDestination.Of(PhoneNumber.TryParse(identifier.Canonical, out PhoneNumber number)
                ? number
                : throw new InvalidOperationException("A held number is canonical already."));

    private async ValueTask<RecordOffer> ShownAsync(SessionId session, CancellationToken cancellationToken) =>
        await offers.FindAsync(session, cancellationToken).ConfigureAwait(false) is OfferToAttach offer
            && !offer.HasExpired(time.GetUtcNow())
            ? offer.Shown
            : RecordOffer.None;

    // REC-ATT-001: a find code goes to one of the account's verified emails or phones.
    private async ValueTask<IReadOnlyList<HeldIdentifier>> ProvableAsync(
        SubjectId account,
        CancellationToken cancellationToken) =>
        [
            .. (await identifiers.HeldAsync(account, cancellationToken).ConfigureAwait(false)).All
                .Where(held => held is { IsVerified: true, IsPending: false, Kind: IdentifierKind.Email or IdentifierKind.Phone }),
        ];

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

    private async ValueTask<Error?> BegunAsync(IdentifierKind kind, CancellationToken cancellationToken)
    {
        Result begun = kind is IdentifierKind.Phone
            ? await work.BeginOutermostAsync(cancellationToken).ConfigureAwait(false)
            : (await work.BeginAsync(cancellationToken).ConfigureAwait(false)).Match(_ => Result.Success(), Result.Failure);

        return begun.Match<Error?>(() => null, error => error);
    }

    // AUTH-ABUSE-004: the code is issued and its message undertaken in one unit of work,
    // so a send the restrictions refuse leaves no code behind it.
    private async ValueTask<Error?> SentAsync(
        byte[] holder,
        SubjectId account,
        HeldIdentifier target,
        string? language,
        string source,
        CancellationToken cancellationToken)
    {
        Error? failure = null;

        string code = (await codes.IssueAsync(holder, cancellationToken).ConfigureAwait(false))
            .Match(issued => issued, error => Held<string>(error, ref failure));

        if (failure is not null)
        {
            return failure;
        }

        Result<SendReference> sent = await sending
            .UndertakeAsync(
                new OutboundMessage(
                    Destination(target),
                    MessageKind.VerificationCode,
                    RestrictionPurpose.Verification,
                    source,
                    language)
                {
                    Subject = account,
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

    private async ValueTask<(Result Answer, bool Commits)> ProvedAsync(
        SessionId session,
        SubjectId account,
        ThrottleAttempt presenting,
        [NeverLogged] string code,
        CancellationToken cancellationToken)
    {
        HeldIdentifier? asked = null;

        foreach (HeldIdentifier held in await ProvableAsync(account, cancellationToken).ConfigureAwait(false))
        {
            if (await codes.OutstandingAsync(Holder(session, held.Id), cancellationToken).ConfigureAwait(false))
            {
                asked = held;

                break;
            }
        }

        // Chapter 09 section 7a: a session that asked for no code names nothing to count
        // against but the source, and is answered as a lapsed code.
        if (asked is null)
        {
            return await CountedAsync(presenting, Error.From(ErrorCodes.CodeExpired), cancellationToken)
                .ConfigureAwait(false);
        }

        var attempt = new ThrottleAttempt(presenting.Source, throttle.Identify(asked.Canonical, usernames: false))
        {
            Account = account,
        };

        if (await DelayedAsync(attempt, cancellationToken).ConfigureAwait(false) is Error delayed)
        {
            return (Result.Failure(delayed), false);
        }

        byte[] holder = Holder(session, asked.Id);

        // AUTH-FACT-004: a wrong code is counted against the source and the value; one
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

        // AUTH-ABUSE-004: the code was presented right, which releases what the send that
        // carried it counted once.
        await restrictions
            .AnsweredAsync(SendReferences.Naming(holder, code), cancellationToken)
            .ConfigureAwait(false);

        return await offering.ProvedAsync(session, [(asked.Kind, asked.Canonical)], cancellationToken)
            .ConfigureAwait(false) is Error unoffered
            ? (Result.Failure(unoffered), false)
            : (Result.Success(), true);
    }

    // CONV-DESIGN-003: a step whose refusal keeps a write is decided in the outermost
    // unit of work, so what the refusal keeps commits whatever a caller does after it.
    private async ValueTask<Result> DecidedAsync(
        Func<ValueTask<(Result Answer, bool Commits)>> decide,
        CancellationToken cancellationToken)
    {
        if ((await work.BeginOutermostAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(() => null, error => error) is Error notBegun)
        {
            return Result.Failure(notBegun);
        }

        (Result decided, bool commits) = await decide().ConfigureAwait(false);

        if (!commits)
        {
            await work.RollbackAsync().ConfigureAwait(false);

            return decided;
        }

        return (await work.CommitAsync(cancellationToken).ConfigureAwait(false))
            .Match(() => decided, Result.Failure);
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

    // REC-ATT-001: each record is judged again under its lock, and each one attached is
    // announced and recorded, the account acting, filed under the record's organization.
    private async ValueTask<Result<int>> AttachedAsync(
        AccessContext context,
        SubjectId account,
        IReadOnlyList<SubjectId> candidates,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        // REC-ATT-001 (D-222): an attachment is admitted only while the account is active,
        // read under its lock in the attachment's transaction, so an attached record never
        // enters a state its account has already left; a restricted account is refused
        // where its restriction is judged.
        await accounts.HoldAsync(account, cancellationToken).ConfigureAwait(false);

        if (await restriction.RefusedAsync(context, cancellationToken).ConfigureAwait(false) is Error restricted)
        {
            return Result.Failure<int>(restricted);
        }

        if (await accounts.StateAsync(account, cancellationToken).ConfigureAwait(false) is not AccountState state)
        {
            return Result.Failure<int>(Error.From(ErrorCodes.Denied));
        }

        if (state is not AccountState.Active)
        {
            return Result.Failure<int>(AccountAdministration.StateConflict(
                state,
                await accounts.SuspendedByAsync(account, cancellationToken).ConfigureAwait(false)));
        }

        int attached = 0;

        foreach (SubjectId record in candidates)
        {
            if (await records.AttachAsync(record, account, now, cancellationToken).ConfigureAwait(false)
                is not OrganizationId organization)
            {
                continue;
            }

            await audit
                .AttachedAsync(account, context.BreakGlassReason, record, organization, now, cancellationToken)
                .ConfigureAwait(false);

            if ((await events
                    .PublishAsync(
                        new RecordAttached(
                            now,
                            string.Create(CultureInfo.InvariantCulture, $"{record.Value}@{account.Value}"),
                            account)
                        {
                            Subject = record,
                            Actor = context.Acting,
                            Effective = context.Effective,
                        },
                        cancellationToken)
                    .ConfigureAwait(false))
                .Match<Error?>(() => null, error => error) is Error unpublished)
            {
                return Result.Failure<int>(unpublished);
            }

            attached++;
        }

        return Result.Success(attached);
    }
}
