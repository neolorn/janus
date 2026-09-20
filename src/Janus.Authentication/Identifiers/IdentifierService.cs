using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Factors;
using Janus.Authentication.Recovery;
using Janus.Authentication.Registration;
using Janus.Authentication.Sending;
using Janus.Authentication.Sessions;
using Janus.Core;
using Janus.Core.Configuration;

namespace Janus.Authentication.Identifiers;

/// <summary>
/// What a live account does with the addresses and numbers it is reached at.
/// </summary>
/// <param name="directory">Where the account's identifiers are read and written.</param>
/// <param name="pending">Where the verifications outstanding are held.</param>
/// <param name="sending">The one path every message takes.</param>
/// <param name="notices">What keeps a holder from being told twice in a window.</param>
/// <param name="sessions">Where the account's sessions are read and ended.</param>
/// <param name="stepUp">What asks whether the session has proved enough.</param>
/// <param name="enrolments">What the enrolment session a browser carries is read from.</param>
/// <param name="configuration">Where the maxima and the windows are read.</param>
/// <param name="work">The transaction each operation writes inside.</param>
/// <param name="events">Where what happened is published.</param>
/// <param name="time">The clock every instant is taken from.</param>
/// <param name="randomness">The randomness the codes and the tokens are drawn from.</param>
/// <remarks>
/// Implements LIB-API-005, REG-IDENT-002 and REG-IDENT-004 to REG-IDENT-007. The
/// verification is the registration's, unchanged: the same code, the same link and
/// the same rule that only a press from the browser that staged the change proves
/// anything. What differs is that an account already exists, so what is added is
/// written to it unverified rather than staged nowhere, and what is given up is held
/// out of reach while its undo lasts.
/// </remarks>
internal sealed class IdentifierService(
    IIdentifierDirectory directory,
    IPendingVerificationStore pending,
    SendingService sending,
    INoticeLedger notices,
    ISessionStore sessions,
    StepUpGuard stepUp,
    EnrolmentSessions enrolments,
    IConfigurationStore configuration,
    IUnitOfWork work,
    IEvents events,
    TimeProvider time,
    RandomNumberGenerator randomness) : IIdentifiers
{
    private static readonly IReadOnlyDictionary<string, string> Nothing =
        new Dictionary<string, string>(capacity: 0, StringComparer.Ordinal);

    /// <inheritdoc/>
    public async ValueTask<Result> AddAsync(
        AccessContext context,
        SessionId session,
        IdentifierKind kind,
        string value,
        string source,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(source);

        if (context.Effective is not SubjectId subject)
        {
            return Result.Failure(Error.From(ErrorCodes.Denied));
        }

        // A username is not an address: it is chosen through the profile and reaches
        // nobody, so nothing here takes one (REG-IDENT-009).
        if (kind is IdentifierKind.Username)
        {
            return Result.Failure(Error.From(ErrorCodes.IdentifierInvalid));
        }

        if (await stepUp
                .PassedAsync(subject, session, StepUpAction.IdentifierAdd, cancellationToken)
                .ConfigureAwait(false)
            is Error closed)
        {
            return Result.Failure(closed);
        }

        if (Canonical(kind, value) is not (string entered, string canonical))
        {
            return Result.Failure(Error.From(ErrorCodes.IdentifierInvalid));
        }

        if (!ScriptMixing.IsSingleScriptPerWord(canonical))
        {
            return Result.Failure(Error.From(ErrorCodes.IdentifierMixedScript));
        }

        Error? failure = null;

        int maximum = (await configuration
                .ReadAsync(Maximum(kind), cancellationToken).ConfigureAwait(false))
            .Match(read => read, error => Withheld<int>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure(failure);
        }

        HeldIdentifiers held = await directory.HeldAsync(subject, cancellationToken)
            .ConfigureAwait(false);

        if (Standing(held, canonical) is null && held.OfKind(kind).Count >= maximum)
        {
            return Result.Failure(Error.From(ErrorCodes.IdentifierMaximum));
        }

        await work.BeginAsync(cancellationToken).ConfigureAwait(false);

        Error? refused = await StageAsync(
                subject, session, held, kind, entered, canonical, maximum, source, cancellationToken)
            .ConfigureAwait(false);

        if (refused is not null)
        {
            return Result.Failure(refused);
        }

        await work.CommitAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }

    /// <inheritdoc/>
    public async ValueTask<Result> VerifyAsync(
        AccessContext context,
        IdentifierId identifier,
        string code,
        string source,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(code);
        ArgumentNullException.ThrowIfNull(source);

        return context.Effective is not SubjectId subject
            ? Result.Failure(Error.From(ErrorCodes.Denied))
            : await ProvedAsync(subject, identifier, code, source, cancellationToken)
                .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<Result> VerifyAsync(
        EnrolmentSessionId enrolment,
        IdentifierId identifier,
        string code,
        string source,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(code);
        ArgumentNullException.ThrowIfNull(source);

        return await enrolments.FindAsync(enrolment, cancellationToken).ConfigureAwait(false)
            is not EnrolmentSession opened
            ? Result.Failure(Error.From(ErrorCodes.EnrolmentTokenInvalid))
            : await ProvedAsync(opened.Subject, identifier, code, source, cancellationToken)
                .ConfigureAwait(false);
    }

    // The code is judged the same way whoever presented it: what differs is only how
    // the account it belongs to was established.
    private async ValueTask<Result> ProvedAsync(
        SubjectId subject,
        IdentifierId identifier,
        string code,
        string source,
        CancellationToken cancellationToken)
    {
        PendingVerification? waiting = await pending
            .FindAsync(identifier, cancellationToken)
            .ConfigureAwait(false);

        if (waiting is null || waiting.Subject != subject || waiting.Staged.IsVerified)
        {
            return Result.Failure(Error.From(ErrorCodes.CodeInvalid));
        }

        StagedIdentity staged = waiting.Staged;

        if (staged.CodeExpiresAt is not DateTimeOffset expires
            || staged.Code is not byte[] outstanding)
        {
            return Result.Failure(Error.From(ErrorCodes.CodeInvalid));
        }

        DateTimeOffset now = time.GetUtcNow();

        if (now >= expires)
        {
            return Result.Failure(Error.From(ErrorCodes.CodeExpired));
        }

        Error? failure = null;

        int cap = (await configuration
                .ReadAsync(Settings.CodeVerificationAttempts, cancellationToken).ConfigureAwait(false))
            .Match(read => read, error => Withheld<int>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure(failure);
        }

        if (staged.CodeSpent || !VerificationCode.Matches(outstanding, code))
        {
            await work.BeginAsync(cancellationToken).ConfigureAwait(false);

            staged.Missed(cap);

            await pending.RecordAsync(waiting, cancellationToken).ConfigureAwait(false);
            await work.CommitAsync(cancellationToken).ConfigureAwait(false);

            return Result.Failure(Error.From(ErrorCodes.CodeInvalid));
        }

        await work.BeginAsync(cancellationToken).ConfigureAwait(false);

        staged.Verify(now);

        await SettleAsync(waiting, now, source, cancellationToken).ConfigureAwait(false);
        await work.CommitAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }

    /// <inheritdoc/>
    public async ValueTask<Result<LinkLanding>> LandAsync(
        SessionId? session,
        string linkToken,
        bool press,
        string source,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(linkToken);
        ArgumentNullException.ThrowIfNull(source);

        if (await WaitingAsync(linkToken, cancellationToken).ConfigureAwait(false)
            is not (PendingVerification waiting, byte[] fingerprint))
        {
            return Result.Failure<LinkLanding>(Error.From(ErrorCodes.CodeInvalid));
        }

        DateTimeOffset now = time.GetUtcNow();

        // The displaced address is not asked to be in any particular browser: what it
        // proves is that whoever answered reads that mailbox, and nothing else
        // confirms a change it is the subject of (REG-IDENT-007).
        if (Displaced(waiting, fingerprint))
        {
            if (!press)
            {
                return Result.Success(new LinkLanding(Verified: false, SameBrowser: false, Code: null));
            }

            await work.BeginAsync(cancellationToken).ConfigureAwait(false);

            waiting.ConfirmOld(now);

            await SettleAsync(waiting, now, source, cancellationToken).ConfigureAwait(false);
            await work.CommitAsync(cancellationToken).ConfigureAwait(false);

            return Result.Success(new LinkLanding(Verified: true, SameBrowser: false, Code: null));
        }

        StagedIdentity staged = waiting.Staged;

        // REG-SESS-003: a press proves the browser only against the session that
        // staged it, so a replace an enrolment session staged is proved by the code
        // alone and never by a press from a browser holding no session.
        bool sameBrowser = waiting.Browser is SessionId staging && session == staging;

        // The press proves it only from the browser that staged the change; anywhere
        // else the page shows the code and changes nothing, which is what defeats a
        // mail scanner's prefetch (REG-SESS-003).
        if (!press || !sameBrowser || staged.IsVerified)
        {
            return Result.Success(new LinkLanding(
                Verified: false,
                sameBrowser,
                sameBrowser || staged.Code is null ? null : VerificationCode.Read(staged.Code)));
        }

        await work.BeginAsync(cancellationToken).ConfigureAwait(false);

        staged.Verify(now);

        await SettleAsync(waiting, now, source, cancellationToken).ConfigureAwait(false);
        await work.CommitAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success(new LinkLanding(Verified: true, SameBrowser: true, Code: null));
    }

    /// <inheritdoc/>
    public async ValueTask<Result> AbandonAsync(string linkToken, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(linkToken);

        // A token that answers to nothing is answered exactly as one that answers to
        // something: the control tells the person nothing either way (REG-SESS-003).
        if (await WaitingAsync(linkToken, cancellationToken).ConfigureAwait(false)
            is not (PendingVerification waiting, byte[] _))
        {
            return Result.Success();
        }

        await work.BeginAsync(cancellationToken).ConfigureAwait(false);

        await pending.RemoveAsync(waiting.Identifier, cancellationToken).ConfigureAwait(false);

        // What an add wrote to the account goes with the verification it was waiting
        // on; what a replace staged was never on the account to begin with.
        if (!waiting.IsReplacement)
        {
            await directory
                .DiscardAsync(waiting.Subject, waiting.Identifier, cancellationToken)
                .ConfigureAwait(false);
        }

        await work.CommitAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }

    /// <inheritdoc/>
    public async ValueTask<Result> MakePrimaryAsync(
        AccessContext context,
        IdentifierId identifier,
        string source,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(source);

        if (context.Effective is not SubjectId subject)
        {
            return Result.Failure(Error.From(ErrorCodes.Denied));
        }

        HeldIdentifiers held = await directory.HeldAsync(subject, cancellationToken)
            .ConfigureAwait(false);

        if (held.Find(identifier) is not HeldIdentifier promoted || !promoted.IsVerified)
        {
            return Result.Failure(Error.From(ErrorCodes.IdentifierInvalid));
        }

        DateTimeOffset now = time.GetUtcNow();

        await work.BeginAsync(cancellationToken).ConfigureAwait(false);

        await directory.PromoteAsync(subject, identifier, cancellationToken).ConfigureAwait(false);

        // REG-IDENT-005: the set as it stood before the change is what is told of it,
        // so a primary that leaves the set still hears that it did.
        _ = await TellAsync(
                held.NoticeSet,
                subject,
                MessageKind.IdentifierSettingsChanged,
                source,
                token: null,
                cancellationToken)
            .ConfigureAwait(false);

        await events
            .PublishAsync(
                new IdentifierPrimaryChanged(now, Key(identifier, now), identifier, promoted.Kind)
                {
                    Subject = subject,
                },
                cancellationToken)
            .ConfigureAwait(false);

        await work.CommitAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }

    /// <inheritdoc/>
    public async ValueTask<Result> SetBackupAsync(
        AccessContext context,
        IdentifierKind kind,
        BackupChoice choice,
        IdentifierId? named,
        string source,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(source);

        if (context.Effective is not SubjectId subject)
        {
            return Result.Failure(Error.From(ErrorCodes.Denied));
        }

        HeldIdentifiers held = await directory.HeldAsync(subject, cancellationToken)
            .ConfigureAwait(false);

        if (Named(held, kind, choice, named) is Error refused)
        {
            return Result.Failure(refused);
        }

        await work.BeginAsync(cancellationToken).ConfigureAwait(false);

        await directory
            .SettleBackupAsync(subject, kind, choice, named, cancellationToken)
            .ConfigureAwait(false);

        _ = await TellAsync(
                held.NoticeSet,
                subject,
                MessageKind.IdentifierSettingsChanged,
                source,
                token: null,
                cancellationToken)
            .ConfigureAwait(false);

        await work.CommitAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }

    /// <inheritdoc/>
    public async ValueTask<Result> RemoveAsync(
        AccessContext context,
        SessionId session,
        IdentifierId identifier,
        string source,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(source);

        if (context.Effective is not SubjectId subject)
        {
            return Result.Failure(Error.From(ErrorCodes.Denied));
        }

        if (await stepUp
                .PassedAsync(subject, session, StepUpAction.IdentifierRemove, cancellationToken)
                .ConfigureAwait(false)
            is Error closed)
        {
            return Result.Failure(closed);
        }

        HeldIdentifiers held = await directory.HeldAsync(subject, cancellationToken)
            .ConfigureAwait(false);

        if (held.Find(identifier) is not HeldIdentifier going)
        {
            return Result.Failure(Error.From(ErrorCodes.IdentifierInvalid));
        }

        if (going.IsPrimary)
        {
            return Result.Failure(Error.From(ErrorCodes.IdentifierPrimary));
        }

        if (await RequiredAsync(going, held, cancellationToken).ConfigureAwait(false) is Error last)
        {
            return Result.Failure(last);
        }

        Error? failure = null;

        TimeSpan window = (await configuration
                .ReadAsync(Settings.IdentifierChangeCoolingOff, cancellationToken).ConfigureAwait(false))
            .Match(read => read, error => Withheld<TimeSpan>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure(failure);
        }

        DateTimeOffset now = time.GetUtcNow();

        await work.BeginAsync(cancellationToken).ConfigureAwait(false);

        await pending.RemoveAsync(identifier, cancellationToken).ConfigureAwait(false);

        if (going.IsVerified)
        {
            await SurrenderAsync(subject, going, held, now, now + window, source, cancellationToken)
                .ConfigureAwait(false);

            // REG-IDENT-006 AC4: every verified identifier signs in, so the account's
            // other sessions end with it. The one asking is left alone.
            await EndOthersAsync(subject, session, now, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            await directory.DiscardAsync(subject, identifier, cancellationToken).ConfigureAwait(false);
        }

        await events
            .PublishAsync(
                new IdentifierRemoved(now, Key(identifier, now), identifier, going.Kind)
                {
                    Subject = subject,
                },
                cancellationToken)
            .ConfigureAwait(false);

        await work.CommitAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }

    /// <inheritdoc/>
    public async ValueTask<Result> UndoAsync(
        string linkToken,
        string source,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(linkToken);
        ArgumentNullException.ThrowIfNull(source);

        GivenUpIdentifier? given = string.IsNullOrWhiteSpace(linkToken)
            ? null
            : await directory
                .GivenUpAsync(OpaqueToken.Of(linkToken).Fingerprint(), cancellationToken)
                .ConfigureAwait(false);

        DateTimeOffset now = time.GetUtcNow();

        // A token that answers to nothing and one whose window has run out are the
        // same answer: neither says whether a removal ever existed.
        if (given is null || now >= given.ExpiresAt)
        {
            return Result.Failure(Error.From(ErrorCodes.ChangeWindowElapsed));
        }

        Error? failure = null;

        int maximum = (await configuration
                .ReadAsync(Maximum(given.Kind), cancellationToken).ConfigureAwait(false))
            .Match(read => read, error => Withheld<int>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure(failure);
        }

        await work.BeginAsync(cancellationToken).ConfigureAwait(false);

        await directory.TakeBackAsync(given.Id, maximum, cancellationToken).ConfigureAwait(false);

        HeldIdentifiers held = await directory.HeldAsync(given.Subject, cancellationToken)
            .ConfigureAwait(false);

        _ = await TellAsync(
                held.NoticeSet,
                given.Subject,
                MessageKind.IdentifierAdded,
                source,
                token: null,
                cancellationToken)
            .ConfigureAwait(false);

        await events
            .PublishAsync(
                new IdentifierAdded(now, Key(given.Id, now), given.Id, given.Kind)
                {
                    Subject = given.Subject,
                },
                cancellationToken)
            .ConfigureAwait(false);

        await work.CommitAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }

    /// <inheritdoc/>
    public async ValueTask<Result> ReplaceAsync(
        AccessContext context,
        SessionId session,
        IdentifierId identifier,
        string value,
        string source,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(source);

        if (context.Effective is not SubjectId subject)
        {
            return Result.Failure(Error.From(ErrorCodes.Denied));
        }

        if (await stepUp
                .PassedAsync(subject, session, StepUpAction.IdentifierAdd, cancellationToken)
                .ConfigureAwait(false)
            is Error closed)
        {
            return Result.Failure(closed);
        }

        return await StagedAsync(subject, session, identifier, value, source, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<Result> ReplaceAsync(
        EnrolmentSessionId enrolment,
        IdentifierId identifier,
        string value,
        string source,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(source);

        if (await enrolments.FindAsync(enrolment, cancellationToken).ConfigureAwait(false)
            is not EnrolmentSession opened)
        {
            return Result.Failure(Error.From(ErrorCodes.EnrolmentTokenInvalid));
        }

        // REG-IDENT-007: the one exception to the displaced address confirming is the
        // mailbox the approver recorded as lost, and an enrolment session opened for
        // anything else reaches this no more than a session does.
        return opened.MailboxLost
            ? await StagedAsync(
                    opened.Subject,
                    session: null,
                    identifier,
                    value,
                    source,
                    cancellationToken)
                .ConfigureAwait(false)
            : Result.Failure(Error.From(ErrorCodes.Denied));
    }

    // AUTH-RECOV-002, REG-IDENT-007: an enrolment session stages from no browser and
    // asks nothing of the address it displaces, because the approver already
    // confirmed on a channel the account holds.
    private async ValueTask<Result> StagedAsync(
        SubjectId subject,
        SessionId? session,
        IdentifierId identifier,
        string value,
        string source,
        CancellationToken cancellationToken)
    {
        HeldIdentifiers held = await directory.HeldAsync(subject, cancellationToken)
            .ConfigureAwait(false);

        if (held.Find(identifier) is not HeldIdentifier changing
            || changing.Kind is IdentifierKind.Username)
        {
            return Result.Failure(Error.From(ErrorCodes.IdentifierInvalid));
        }

        if (changing.IsLocked)
        {
            return Result.Failure(Error.From(ErrorCodes.IdentifierLocked));
        }

        Error? failure = null;

        int maximum = (await configuration
                .ReadAsync(Maximum(changing.Kind), cancellationToken).ConfigureAwait(false))
            .Match(read => read, error => Withheld<int>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure(failure);
        }

        // REG-IDENT-007 is the change of single-address mode. Where the deployment
        // holds several of a kind, a change is an add and a remove, each of which
        // says to the account what it is doing.
        if (maximum is not 1)
        {
            return Result.Failure(Error.From(ErrorCodes.IdentifierInvalid));
        }

        if (Canonical(changing.Kind, value) is not (string entered, string canonical))
        {
            return Result.Failure(Error.From(ErrorCodes.IdentifierInvalid));
        }

        if (!ScriptMixing.IsSingleScriptPerWord(canonical))
        {
            return Result.Failure(Error.From(ErrorCodes.IdentifierMixedScript));
        }

        if (await pending.FindAsync(identifier, cancellationToken).ConfigureAwait(false) is not null)
        {
            return Result.Failure(Error.From(ErrorCodes.ChangePending));
        }

        await work.BeginAsync(cancellationToken).ConfigureAwait(false);

        if (await TakenAsync(subject, changing.Kind, canonical, source, cancellationToken)
            .ConfigureAwait(false))
        {
            await work.CommitAsync(cancellationToken).ConfigureAwait(false);

            return Result.Success();
        }

        // REG-IDENT-007: the old address is asked only where nothing else could undo
        // a hostile change, which is when the account has no other channel at all.
        var waiting = PendingVerification.ToReplace(
            subject,
            session,
            StagedIdentity.Of(identifier, changing.Kind, entered, canonical),
            session is not null && held.NoticeSetWithout(identifier).Count is 0,
            time.GetUtcNow());

        await pending.AddAsync(waiting, cancellationToken).ConfigureAwait(false);

        if (await SendCodeAsync(waiting, source, cancellationToken).ConfigureAwait(false)
            is Error refused)
        {
            return Result.Failure(refused);
        }

        if (waiting.OldMustConfirm
            && await AskOldAsync(waiting, changing, source, cancellationToken).ConfigureAwait(false)
                is Error asked)
        {
            return Result.Failure(asked);
        }

        await pending.RecordAsync(waiting, cancellationToken).ConfigureAwait(false);
        await work.CommitAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }

    private static IntegerSetting Maximum(IdentifierKind kind) =>
        kind is IdentifierKind.Email ? Settings.IdentifiersEmailMax : Settings.IdentifiersPhoneMax;

    private static string Key(IdentifierId identifier, DateTimeOffset at) =>
        string.Create(CultureInfo.InvariantCulture, $"{identifier.Value}@{at.UtcTicks}");

    private static bool Displaced(PendingVerification waiting, byte[] fingerprint) =>
        waiting.OldLink is byte[] link
        && CryptographicOperations.FixedTimeEquals(link, fingerprint);

    private static HeldIdentifier? Standing(HeldIdentifiers held, string canonical)
    {
        foreach (HeldIdentifier identifier in held.All)
        {
            if (string.Equals(identifier.Canonical, canonical, StringComparison.Ordinal))
            {
                return identifier;
            }
        }

        return null;
    }

    private static Error? Named(
        HeldIdentifiers held,
        IdentifierKind kind,
        BackupChoice choice,
        IdentifierId? named)
    {
        if (choice is not BackupChoice.Named)
        {
            return named is null ? null : Error.From(ErrorCodes.IdentifierInvalid);
        }

        if (named is not IdentifierId id
            || held.Find(id) is not HeldIdentifier backup
            || backup.Kind != kind
            || !backup.IsVerified)
        {
            return Error.From(ErrorCodes.IdentifierInvalid);
        }

        // REG-IDENT-002: the primary cannot be the backup, because a setting that
        // named it would leave the set at the primary alone without saying so.
        return backup.IsPrimary ? Error.From(ErrorCodes.IdentifierPrimary) : null;
    }

    private static (string Entered, string Canonical)? Canonical(IdentifierKind kind, string value)
    {
        string entered = value.Trim();

        if (kind is IdentifierKind.Email)
        {
            return EmailAddress.TryParse(entered, out EmailAddress address)
                ? (entered, address.Value)
                : null;
        }

        return PhoneNumber.TryParse(entered, out PhoneNumber number)
            ? (entered, number.Value)
            : null;
    }

    private static SendDestination Destination(IdentifierKind kind, string canonical) =>
        kind is IdentifierKind.Email
            ? SendDestination.Of(Address(canonical))
            : SendDestination.Of(Number(canonical));

    private static EmailAddress Address(string canonical) =>
        EmailAddress.TryParse(canonical, out EmailAddress address)
            ? address
            : throw new InvalidOperationException("A held address is canonical already.");

    private static PhoneNumber Number(string canonical) =>
        PhoneNumber.TryParse(canonical, out PhoneNumber number)
            ? number
            : throw new InvalidOperationException("A held number is canonical already.");

    private static TValue Withheld<TValue>(Error error, ref Error? failure)
    {
        failure = error;

        return default!;
    }

    private async ValueTask<(PendingVerification Waiting, byte[] Fingerprint)?> WaitingAsync(
        string linkToken,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(linkToken))
        {
            return null;
        }

        byte[] fingerprint = OpaqueToken.Of(linkToken).Fingerprint();

        PendingVerification? waiting = await pending
            .FindByLinkAsync(fingerprint, cancellationToken)
            .ConfigureAwait(false);

        return waiting is null ? null : (waiting, fingerprint);
    }

    // IDN-ATTR-001: a message sent without a request in front of it goes out in the
    // language the account settled on, and in the deployment's first where it has
    // settled none.
    private async ValueTask<string> LanguageAsync(SubjectId subject, CancellationToken cancellationToken)
    {
        string? settled = await directory.LanguageAsync(subject, cancellationToken)
            .ConfigureAwait(false);

        if (settled is not null)
        {
            return settled;
        }

        IReadOnlyList<string> languages = (await configuration
                .ReadAsync(Settings.NotificationLanguages, cancellationToken).ConfigureAwait(false))
            .Match(read => read, _ => (IReadOnlyList<string>)[]);

        return languages.Count > 0 ? languages[0] : string.Empty;
    }

    // Whether the kind would be left under the minimum the deployment asks of it. An
    // account keeps one verified email always, and one verified phone while
    // registration.phone is required (REG-IDENT-001, REG-IDENT-006).
    private async ValueTask<Error?> RequiredAsync(
        HeldIdentifier going,
        HeldIdentifiers held,
        CancellationToken cancellationToken)
    {
        if (!going.IsVerified || going.Kind is IdentifierKind.Username)
        {
            return null;
        }

        int minimum = 1;

        if (going.Kind is IdentifierKind.Phone)
        {
            Error? failure = null;

            AttributeRequirement phones = (await configuration
                    .ReadAsync(Settings.RegistrationPhone, cancellationToken).ConfigureAwait(false))
                .Match(read => read, error => Withheld<AttributeRequirement>(error, ref failure));

            if (failure is not null)
            {
                return failure;
            }

            minimum = phones is AttributeRequirement.Required ? 1 : 0;
        }

        return held.Verified(going.Kind) - 1 < minimum
            ? Error.From(ErrorCodes.IdentifierLastOfKind)
            : null;
    }

    // The fresh case and the case where the value is out of reach are one path: the
    // caller cannot tell which happened and nothing is written either way
    // (REG-SESS-005, REG-IDENT-004).
    private async ValueTask<bool> TakenAsync(
        SubjectId subject,
        IdentifierKind kind,
        string canonical,
        string source,
        CancellationToken cancellationToken)
    {
        SubjectId? owner = await directory
            .OwnerAsync(kind, canonical, cancellationToken)
            .ConfigureAwait(false);

        if (owner is SubjectId holder && holder != subject)
        {
            _ = await TellHolderAsync(kind, canonical, holder, source, cancellationToken)
                .ConfigureAwait(false);

            return true;
        }

        return owner is not null
            || await directory
                .IsReservedAsync(kind, canonical, time.GetUtcNow(), cancellationToken)
                .ConfigureAwait(false);
    }

    private async ValueTask<Error?> StageAsync(
        SubjectId subject,
        SessionId session,
        HeldIdentifiers held,
        IdentifierKind kind,
        string entered,
        string canonical,
        int maximum,
        string source,
        CancellationToken cancellationToken)
    {
        // A value the account already holds unverified is the one it is waiting on,
        // so asking again sends again rather than starting a second wait.
        if (Standing(held, canonical) is HeldIdentifier standing)
        {
            PendingVerification? again = standing.IsVerified
                ? null
                : await pending.FindAsync(standing.Id, cancellationToken).ConfigureAwait(false);

            if (again is null)
            {
                return null;
            }

            if (await SendCodeAsync(again, source, cancellationToken).ConfigureAwait(false)
                is Error refused)
            {
                return refused;
            }

            await pending.RecordAsync(again, cancellationToken).ConfigureAwait(false);

            return null;
        }

        if (await TakenAsync(subject, kind, canonical, source, cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        var id = IdentifierId.New(time);
        DateTimeOffset now = time.GetUtcNow();

        await directory
            .TakeOnAsync(subject, id, kind, entered, canonical, now, maximum, cancellationToken)
            .ConfigureAwait(false);

        var staged = PendingVerification.ToAdd(
            subject,
            session,
            StagedIdentity.Of(id, kind, entered, canonical),
            now);

        await pending.AddAsync(staged, cancellationToken).ConfigureAwait(false);

        if (await SendCodeAsync(staged, source, cancellationToken).ConfigureAwait(false)
            is Error sending)
        {
            return sending;
        }

        await pending.RecordAsync(staged, cancellationToken).ConfigureAwait(false);

        // REG-IDENT-004 AC3: the set as it stands hears of every addition, once.
        _ = await TellAsync(
                held.NoticeSet,
                subject,
                MessageKind.IdentifierAdded,
                source,
                token: null,
                cancellationToken)
            .ConfigureAwait(false);

        return null;
    }

    private async ValueTask<Error?> SendCodeAsync(
        PendingVerification waiting,
        string source,
        CancellationToken cancellationToken)
    {
        Error? failure = null;

        TimeSpan lifetime = (await configuration
                .ReadAsync(Settings.CodeVerificationLifetime, cancellationToken).ConfigureAwait(false))
            .Match(read => read, error => Withheld<TimeSpan>(error, ref failure));

        if (failure is not null)
        {
            return failure;
        }

        StagedIdentity staged = waiting.Staged;
        string code = VerificationCode.Draw(randomness);
        var link = OpaqueToken.Draw(randomness);

        Result<SendReference> sent = await sending
            .SendAsync(
                new SendRequest(
                    Destination(staged.Kind, staged.Canonical),
                    MessageKind.VerificationCode,
                    RestrictionPurpose.Verification,
                    source,
                    await LanguageAsync(waiting.Subject, cancellationToken).ConfigureAwait(false))
                {
                    Subject = waiting.Subject,
                    Values = new Dictionary<string, string>(capacity: 2, StringComparer.Ordinal)
                    {
                        ["code"] = code,
                        ["token"] = link.Value,
                    },
                },
                cancellationToken)
            .ConfigureAwait(false);

        if (sent.Match(_ => (Error?)null, error => error) is Error refused)
        {
            return refused;
        }

        staged.Sent(VerificationCode.Held(code), link.Fingerprint(), time.GetUtcNow() + lifetime);

        return null;
    }

    private async ValueTask<Error?> AskOldAsync(
        PendingVerification waiting,
        HeldIdentifier displaced,
        string source,
        CancellationToken cancellationToken)
    {
        var link = OpaqueToken.Draw(randomness);

        Result<SendReference> sent = await sending
            .SendAsync(
                new SendRequest(
                    Destination(displaced.Kind, displaced.Canonical),
                    MessageKind.IdentifierChangeConfirm,
                    RestrictionPurpose.Notification,
                    source,
                    await LanguageAsync(waiting.Subject, cancellationToken).ConfigureAwait(false))
                {
                    Subject = waiting.Subject,
                    Values = new Dictionary<string, string>(capacity: 1, StringComparer.Ordinal)
                    {
                        ["token"] = link.Value,
                    },
                },
                cancellationToken)
            .ConfigureAwait(false);

        if (sent.Match(_ => (Error?)null, error => error) is Error refused)
        {
            return refused;
        }

        waiting.AskedOld(link.Fingerprint());

        return null;
    }

    private async ValueTask<int> TellHolderAsync(
        IdentifierKind kind,
        string canonical,
        SubjectId holder,
        string source,
        CancellationToken cancellationToken)
    {
        Error? failure = null;

        TimeSpan window = (await configuration
                .ReadAsync(Settings.AbuseNonexistentWindow, cancellationToken).ConfigureAwait(false))
            .Match(read => read, error => Withheld<TimeSpan>(error, ref failure));

        if (failure is not null
            || !await notices
                .FirstAsync(canonical, time.GetUtcNow(), window, cancellationToken)
                .ConfigureAwait(false))
        {
            return 0;
        }

        // A refusal to tell the holder is not a refusal of the operation: the caller
        // sees the same outcome either way (REG-SESS-005 AC1).
        Result<SendReference> sent = await sending
            .SendAsync(
                new SendRequest(
                    Destination(kind, canonical),
                    MessageKind.AccountExists,
                    RestrictionPurpose.Notification,
                    source,
                    await LanguageAsync(holder, cancellationToken).ConfigureAwait(false))
                {
                    Subject = holder,
                },
                cancellationToken)
            .ConfigureAwait(false);

        return sent.Match(_ => 1, _ => 0);
    }

    private async ValueTask<int> TellAsync(
        IReadOnlyList<HeldIdentifier> reached,
        SubjectId subject,
        MessageKind message,
        string source,
        string? token,
        CancellationToken cancellationToken)
    {
        if (reached.Count is 0)
        {
            return 0;
        }

        string language = await LanguageAsync(subject, cancellationToken).ConfigureAwait(false);
        int told = 0;

        foreach (HeldIdentifier identifier in reached)
        {
            var request = new SendRequest(
                Destination(identifier.Kind, identifier.Canonical),
                message,
                RestrictionPurpose.Notification,
                source,
                language)
            {
                Subject = subject,
                Values = token is null
                    ? Nothing
                    : new Dictionary<string, string>(capacity: 1, StringComparer.Ordinal)
                    {
                        ["token"] = token,
                    },
            };

            // A security notice one destination refuses still reaches the rest: the
            // set exists so that no one channel can silence it.
            Result<SendReference> sent = await sending
                .SendAsync(request, cancellationToken)
                .ConfigureAwait(false);

            told += sent.Match(_ => 1, _ => 0);
        }

        return told;
    }

    private async ValueTask SurrenderAsync(
        SubjectId subject,
        HeldIdentifier going,
        HeldIdentifiers held,
        DateTimeOffset now,
        DateTimeOffset expiresAt,
        string source,
        CancellationToken cancellationToken)
    {
        var undo = OpaqueToken.Draw(randomness);

        await directory
            .GiveUpAsync(subject, going.Id, now, expiresAt, undo.Fingerprint(), cancellationToken)
            .ConfigureAwait(false);

        // REG-IDENT-006: the undo reaches what remains and never the address that was
        // removed, so a mailbox somebody else holds cannot re-attach itself.
        _ = await TellAsync(
                held.NoticeSetWithout(going.Id),
                subject,
                MessageKind.IdentifierRemoved,
                source,
                undo.Value,
                cancellationToken)
            .ConfigureAwait(false);

        _ = await TellAsync(
                [going],
                subject,
                MessageKind.IdentifierDetached,
                source,
                token: null,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async ValueTask EndOthersAsync(
        SubjectId subject,
        SessionId keeping,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<Session> live = await sessions
            .LiveOfAsync(subject, now, cancellationToken)
            .ConfigureAwait(false);

        SessionId? kept = null;

        foreach (Session session in live)
        {
            if (session.Id == keeping)
            {
                kept = session.Spine;
            }
        }

        var ended = new HashSet<SessionId>();

        foreach (Session session in live)
        {
            if (session.Spine != kept && ended.Add(session.Spine))
            {
                await sessions.EndSpineAsync(session.Spine, now, cancellationToken)
                    .ConfigureAwait(false);
            }
        }
    }

    // What a completed verification does to the account: an add proves the identifier
    // it wrote, a replace swaps the value of the one it named and holds the displaced
    // value for the undo.
    private async ValueTask SettleAsync(
        PendingVerification waiting,
        DateTimeOffset now,
        string source,
        CancellationToken cancellationToken)
    {
        if (!waiting.IsSettled)
        {
            await pending.RecordAsync(waiting, cancellationToken).ConfigureAwait(false);

            return;
        }

        StagedIdentity staged = waiting.Staged;

        if (waiting.IsReplacement)
        {
            await SwapAsync(waiting, now, source, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            await directory
                .ProveAsync(waiting.Subject, staged.Id, now, cancellationToken)
                .ConfigureAwait(false);
        }

        await pending.RemoveAsync(staged.Id, cancellationToken).ConfigureAwait(false);

        await events
            .PublishAsync(
                new IdentifierAdded(now, Key(staged.Id, now), staged.Id, staged.Kind)
                {
                    Subject = waiting.Subject,
                },
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async ValueTask SwapAsync(
        PendingVerification waiting,
        DateTimeOffset now,
        string source,
        CancellationToken cancellationToken)
    {
        Error? failure = null;

        TimeSpan window = (await configuration
                .ReadAsync(Settings.IdentifierChangeCoolingOff, cancellationToken).ConfigureAwait(false))
            .Match(read => read, error => Withheld<TimeSpan>(error, ref failure));

        HeldIdentifiers held = await directory
            .HeldAsync(waiting.Subject, cancellationToken)
            .ConfigureAwait(false);

        StagedIdentity staged = waiting.Staged;

        if (failure is not null || held.Find(staged.Id) is not HeldIdentifier displaced)
        {
            return;
        }

        var undo = OpaqueToken.Draw(randomness);

        await directory
            .ReplaceAsync(
                waiting.Subject,
                staged.Id,
                staged.Entered,
                staged.Canonical,
                now,
                now + window,
                undo.Fingerprint(),
                cancellationToken)
            .ConfigureAwait(false);

        // REG-IDENT-007: the undo goes to the channels the account still has, which
        // is every member of the set but the one the swap displaced.
        _ = await TellAsync(
                held.NoticeSetWithout(displaced.Id),
                waiting.Subject,
                MessageKind.IdentifierRemoved,
                source,
                undo.Value,
                cancellationToken)
            .ConfigureAwait(false);
    }
}
