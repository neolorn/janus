using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Accounts;
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
/// <param name="restriction">Whether the account's processing is restricted, as the gate answers it.</param>
/// <param name="pending">Where the verifications outstanding are held.</param>
/// <param name="codes">Where the code that verifies a staged value is issued and answered.</param>
/// <param name="sending">The one path every message takes.</param>
/// <param name="restrictions">What an ask that sends nothing is counted against.</param>
/// <param name="landing">Where a link the message carries lands.</param>
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
/// held on a pending verification the account lists unverified and written to it only
/// when it verifies, and what is given up is held out of reach while its undo lasts.
/// </remarks>
internal sealed class IdentifierService(
    IIdentifierDirectory directory,
    ISettingsRestriction restriction,
    IPendingVerificationStore pending,
    VerificationCodes codes,
    IGovernedSend sending,
    ISendingRestrictions restrictions,
    LandingLinks landing,
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

        if (StepUpGuard.RefusedInBreakGlass(context, StepUpAction.IdentifierAdd) is Error withheld)
        {
            return Result.Failure(withheld);
        }

        // IDN-ACCT-007 AC2: a restricted account changes none of its settings.
        if (await restriction.RefusedAsync(context, cancellationToken).ConfigureAwait(false)
            is Error restricted)
        {
            return Result.Failure(restricted);
        }

        // A username is not an address: it is chosen through the profile and reaches
        // nobody, so nothing here takes one (REG-IDENT-009).
        if (kind is IdentifierKind.Username)
        {
            return Result.Failure(Error.From(ErrorCodes.IdentifierInvalid));
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

        // D-178: the step-up is judged after every other refusal the addition can give
        // before its transaction.
        if (await stepUp
                .PassedAsync(subject, session, StepUpAction.IdentifierAdd, cancellationToken)
                .ConfigureAwait(false)
            is Error closed)
        {
            return Result.Failure(closed);
        }

        if ((await work.BeginAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(() => null, error => error) is Error notBegun)
        {
            return Result.Failure(notBegun);
        }

        // D-166 X3: the set is read again under its lock, so of two additions at once
        // the second counts what the first took on.
        await directory.HoldAsync(subject, cancellationToken).ConfigureAwait(false);

        // AUTHZ-GATE-006, D-183: the restriction is asked again under that lock, which the
        // operation takes itself before any other, so one committed since the gate step
        // refuses the addition before anything is written.
        if (await restriction.RefusedAsync(context, cancellationToken).ConfigureAwait(false) is Error since)
        {
            await work.RollbackAsync().ConfigureAwait(false);

            return Result.Failure(since);
        }

        held = await directory.HeldAsync(subject, cancellationToken).ConfigureAwait(false);

        if (Standing(held, canonical) is null && held.OfKind(kind).Count >= maximum)
        {
            await work.RollbackAsync().ConfigureAwait(false);

            return Result.Failure(Error.From(ErrorCodes.IdentifierMaximum));
        }

        // REG-SESS-005, CONV-DESIGN-003: whether the value is held or reserved is judged
        // under its lock, taken after the set's and before the code's send is counted.
        await directory.LockValuesAsync([(kind, canonical)], cancellationToken).ConfigureAwait(false);

        (Error? refused, bool staged) = await StageAsync(
                subject, session, held, kind, entered, canonical, source, cancellationToken)
            .ConfigureAwait(false);

        if (refused is not null)
        {
            await work.RollbackAsync().ConfigureAwait(false);

            return Result.Failure(refused);
        }

        // CONV-DESIGN-003: an addition of a value the account holds already wrote
        // nothing, so its unit of work is rolled back and answered as any other.
        if (!staged)
        {
            await work.RollbackAsync().ConfigureAwait(false);

            return Result.Success();
        }

        if ((await work.CommitAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(() => null, error => error) is Error notCommitted)
        {
            return Result.Failure(notCommitted);
        }

        return Result.Success();
    }

    /// <inheritdoc/>
    public async ValueTask<Result> VerifyAsync(
        AccessContext context,
        SessionId session,
        IdentifierId identifier,
        [NeverLogged] string code,
        string source,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(code);
        ArgumentNullException.ThrowIfNull(source);

        if (context.Effective is not SubjectId subject)
        {
            return Result.Failure(Error.From(ErrorCodes.Denied));
        }

        // IDN-ACCT-007 AC2: a restricted account changes none of its settings.
        if (await restriction.RefusedAsync(context, cancellationToken).ConfigureAwait(false)
            is Error restricted)
        {
            return Result.Failure(restricted);
        }

        return await ProvedAsync(context, subject, session, identifier, code, source, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<Result> VerifyAsync(
        EnrolmentSessionId enrolment,
        IdentifierId identifier,
        [NeverLogged] string code,
        string source,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(code);
        ArgumentNullException.ThrowIfNull(source);

        return await enrolments.FindAsync(enrolment, cancellationToken).ConfigureAwait(false)
            is not EnrolmentSession opened
            ? Result.Failure(Error.From(ErrorCodes.EnrolmentTokenInvalid))
            : await ProvedAsync(
                    asking: null,
                    opened.Subject,
                    completing: null,
                    identifier,
                    code,
                    source,
                    cancellationToken)
                .ConfigureAwait(false);
    }

    // The code is judged the same way whoever presented it: what differs is only how
    // the account it belongs to was established, and the session, where there is one,
    // that a replacement it completes keeps (IDN-LIFE-008).
    private async ValueTask<Result> ProvedAsync(
        AccessContext? asking,
        SubjectId subject,
        SessionId? completing,
        IdentifierId identifier,
        [NeverLogged] string code,
        string source,
        CancellationToken cancellationToken)
    {
        DateTimeOffset now = time.GetUtcNow();

        if ((await work.BeginAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(() => null, error => error) is Error notBegun)
        {
            return Result.Failure(notBegun);
        }

        // AUTHZ-GATE-006, D-183: the gate is asked again inside the unit of work, with the
        // acting account's row held before any other lock, so a restriction committed since
        // the gate step refuses the verification before anything is written.
        if (await RestrictedSinceAsync(asking, cancellationToken).ConfigureAwait(false) is Error since)
        {
            await work.RollbackAsync().ConfigureAwait(false);

            return Result.Failure(since);
        }

        // REG-IDENT-004, CONV-DESIGN-003: a verification writes an identifier to the
        // set, so the set's lock is taken before the verification's, the code's and the
        // value's, as an addition takes them.
        await directory.HoldAsync(subject, cancellationToken).ConfigureAwait(false);

        // D-166 X3: the verification is read under its lock, so every wrong code of many
        // at once is counted, and of two right ones only the first proves it.
        PendingVerification? waiting = await pending
            .FindForUpdateAsync(identifier, cancellationToken)
            .ConfigureAwait(false);

        if (waiting is null || waiting.Subject != subject || waiting.Staged.IsVerified)
        {
            await work.RollbackAsync().ConfigureAwait(false);

            return Result.Failure(Error.From(ErrorCodes.CodeInvalid));
        }

        StagedIdentity staged = waiting.Staged;

        // AUTH-FACT-004: the try is read, compared and counted on the verification-code
        // record under its lock.
        Result presented = await codes
            .PresentAsync(PendingVerification.CodeHolder(identifier), code, cancellationToken)
            .ConfigureAwait(false);

        if (presented.Match(() => (Error?)null, error => error) is Error refused)
        {
            if (refused.Code != ErrorCodes.CodeInvalid)
            {
                await work.RollbackAsync().ConfigureAwait(false);

                return Result.Failure(refused);
            }

            // AUTH-FACT-004, CONV-DESIGN-003: the wrong try is counted on the code's
            // record whatever the outcome, so this refusal commits the count alone.
            return (await work.CommitAsync(cancellationToken).ConfigureAwait(false))
                .Match(() => Result.Failure(refused), Result.Failure);
        }

        staged.Verify(now);

        Result settled = await SettleAsync(waiting, completing, now, source, cancellationToken)
            .ConfigureAwait(false);

        if (settled.Match(() => (Error?)null, error => error) is Error unsettled)
        {
            await work.RollbackAsync().ConfigureAwait(false);

            return Result.Failure(unsettled);
        }

        if ((await work.CommitAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(() => null, error => error) is Error notCommitted)
        {
            return Result.Failure(notCommitted);
        }

        return Result.Success();
    }

    /// <inheritdoc/>
    public async ValueTask<Result<LinkLanding>> LandAsync(
        SessionId? session,
        [NeverLogged] string linkToken,
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

            if ((await work.BeginAsync(cancellationToken).ConfigureAwait(false))
                .Match<Error?>(() => null, error => error) is Error notBegunAgain)
            {
                return Result.Failure<LinkLanding>(notBegunAgain);
            }

            // CONV-DESIGN-003: the set's lock first, as for a code.
            await directory.HoldAsync(waiting.Subject, cancellationToken).ConfigureAwait(false);

            // D-166 X3: the confirmation is written on the verification as read under its
            // lock, so a code proved at the same moment is seen and the change is applied
            // by whichever of the two comes second; one settled meanwhile has no row.
            if (await pending.FindForUpdateAsync(waiting.Identifier, cancellationToken).ConfigureAwait(false)
                is not PendingVerification confirming)
            {
                await work.RollbackAsync().ConfigureAwait(false);

                return Result.Failure<LinkLanding>(Error.From(ErrorCodes.CodeInvalid));
            }

            waiting = confirming;

            // REG-IDENT-007, D-187: the confirmation answers from a record of its own,
            // read under its lock after the verification's. A press past its lifetime
            // changes nothing, so its unit of work is rolled back (CONV-DESIGN-003).
            Result pressed = await codes
                .PressAsync(PendingVerification.ConfirmationHolder(waiting.Identifier), cancellationToken)
                .ConfigureAwait(false);

            if (pressed.Match(() => (Error?)null, error => error) is Error lapsed)
            {
                await work.RollbackAsync().ConfigureAwait(false);

                return Result.Failure<LinkLanding>(lapsed);
            }

            waiting.ConfirmOld(now);

            // IDN-LIFE-008 AC1: the displaced address confirms from no session of the
            // account, so a replacement it completes keeps none.
            Result settled = await SettleAsync(waiting, completing: null, now, source, cancellationToken)
                .ConfigureAwait(false);

            if (settled.Match(() => (Error?)null, error => error) is Error unsettled)
            {
                await work.RollbackAsync().ConfigureAwait(false);

                return Result.Failure<LinkLanding>(unsettled);
            }

            if ((await work.CommitAsync(cancellationToken).ConfigureAwait(false))
                .Match<Error?>(() => null, error => error) is Error notCommittedAgain)
            {
                return Result.Failure<LinkLanding>(notCommittedAgain);
            }

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
                sameBrowser
                    ? null
                    : await codes.ShownAsync(PendingVerification.CodeHolder(staged.Id), cancellationToken).ConfigureAwait(false)));
        }

        if ((await work.BeginAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(() => null, error => error) is Error notBegun)
        {
            return Result.Failure<LinkLanding>(notBegun);
        }

        // CONV-DESIGN-003: the set's lock first, as for a code.
        await directory.HoldAsync(waiting.Subject, cancellationToken).ConfigureAwait(false);

        // D-166 X3: as for the displaced address's confirmation; a press that finds the
        // value proved meanwhile changes nothing, as the press would have before it.
        if (await pending.FindForUpdateAsync(waiting.Identifier, cancellationToken).ConfigureAwait(false)
            is not PendingVerification pressing)
        {
            await work.RollbackAsync().ConfigureAwait(false);

            return Result.Failure<LinkLanding>(Error.From(ErrorCodes.CodeInvalid));
        }

        waiting = pressing;
        staged = waiting.Staged;

        if (staged.IsVerified)
        {
            return (await work.CommitAsync(cancellationToken).ConfigureAwait(false))
                .Match(() => Result.Success(new LinkLanding(Verified: false, sameBrowser, Code: null)), Result.Failure<LinkLanding>);
        }

        // The press proved the value, so the code that would have is ended with it.
        await codes.EndAsync(PendingVerification.CodeHolder(staged.Id), cancellationToken).ConfigureAwait(false);

        staged.Verify(now);

        Result landed = await SettleAsync(waiting, session, now, source, cancellationToken)
            .ConfigureAwait(false);

        if (landed.Match(() => (Error?)null, error => error) is Error unlanded)
        {
            await work.RollbackAsync().ConfigureAwait(false);

            return Result.Failure<LinkLanding>(unlanded);
        }

        if ((await work.CommitAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(() => null, error => error) is Error notCommitted)
        {
            return Result.Failure<LinkLanding>(notCommitted);
        }

        return Result.Success(new LinkLanding(Verified: true, SameBrowser: true, Code: null));
    }

    /// <inheritdoc/>
    public async ValueTask<Result> AbandonAsync([NeverLogged] string linkToken, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(linkToken);

        // A token that answers to nothing is answered exactly as one that answers to
        // something: the control tells the person nothing either way (REG-SESS-003).
        if (await WaitingAsync(linkToken, cancellationToken).ConfigureAwait(false)
            is not (PendingVerification waiting, byte[] _))
        {
            return Result.Success();
        }

        if ((await work.BeginAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(() => null, error => error) is Error notBegun)
        {
            return Result.Failure(notBegun);
        }

        // REG-IDENT-004, REG-IDENT-007: what an add or a replace staged is held on its
        // pending verification alone, so ending that and its records leaves the account
        // as it stood.
        await EndRecordsAsync(waiting.Identifier, cancellationToken).ConfigureAwait(false);
        await pending.RemoveAsync(waiting.Identifier, cancellationToken).ConfigureAwait(false);

        if ((await work.CommitAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(() => null, error => error) is Error notCommitted)
        {
            return Result.Failure(notCommitted);
        }

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

        // IDN-ACCT-007 AC2: a restricted account changes none of its settings.
        if (await restriction.RefusedAsync(context, cancellationToken).ConfigureAwait(false)
            is Error restricted)
        {
            return Result.Failure(restricted);
        }

        HeldIdentifiers held = await directory.HeldAsync(subject, cancellationToken)
            .ConfigureAwait(false);

        if (Unpromotable(held.Find(identifier)) is Error refused)
        {
            return Result.Failure(refused);
        }

        DateTimeOffset now = time.GetUtcNow();

        if ((await work.BeginAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(() => null, error => error) is Error notBegun)
        {
            return Result.Failure(notBegun);
        }

        // D-166 X3: decided again on the set under its lock, so of two promotions at
        // once, or a promotion and a removal, the second decides on what the first left.
        await directory.HoldAsync(subject, cancellationToken).ConfigureAwait(false);

        // AUTHZ-GATE-006, D-183: the restriction is asked again under that lock, which the
        // operation takes itself before any other, so one committed since the gate step
        // refuses the promotion before anything is written.
        if (await restriction.RefusedAsync(context, cancellationToken).ConfigureAwait(false) is Error since)
        {
            await work.RollbackAsync().ConfigureAwait(false);

            return Result.Failure(since);
        }

        held = await directory.HeldAsync(subject, cancellationToken).ConfigureAwait(false);

        if (Unpromotable(held.Find(identifier)) is Error moved)
        {
            await work.RollbackAsync().ConfigureAwait(false);

            return Result.Failure(moved);
        }

        HeldIdentifier promoted = held.Find(identifier)!;

        await directory.PromoteAsync(subject, identifier, cancellationToken).ConfigureAwait(false);

        // REG-IDENT-005: the set as it stood before the change is what is told of it,
        // so a primary that leaves the set still hears that it did.
        _ = await TellAsync(
                held.NoticeSet,
                subject,
                MessageKind.IdentifierSettingsChanged,
                source,
                link: null,
                cancellationToken)
            .ConfigureAwait(false);

        Result published = await events
            .PublishAsync(
                new IdentifierPrimaryChanged(now, Key(identifier, now), identifier, promoted.Kind)
                {
                    Subject = subject,
                },
                cancellationToken)
            .ConfigureAwait(false);

        if (published.Match(() => (Error?)null, error => error) is Error unpublished)
        {
            await work.RollbackAsync().ConfigureAwait(false);

            return Result.Failure(unpublished);
        }

        if ((await work.CommitAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(() => null, error => error) is Error notCommitted)
        {
            return Result.Failure(notCommitted);
        }

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

        // IDN-ACCT-007 AC2: a restricted account changes none of its settings.
        if (await restriction.RefusedAsync(context, cancellationToken).ConfigureAwait(false)
            is Error restricted)
        {
            return Result.Failure(restricted);
        }

        HeldIdentifiers held = await directory.HeldAsync(subject, cancellationToken)
            .ConfigureAwait(false);

        if (Named(held, kind, choice, named) is Error refused)
        {
            return Result.Failure(refused);
        }

        if ((await work.BeginAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(() => null, error => error) is Error notBegun)
        {
            return Result.Failure(notBegun);
        }

        // D-166 X3: as for a promotion.
        await directory.HoldAsync(subject, cancellationToken).ConfigureAwait(false);

        // AUTHZ-GATE-006, D-183: the restriction is asked again under that lock, which the
        // operation takes itself before any other, so one committed since the gate step
        // refuses the setting before anything is written.
        if (await restriction.RefusedAsync(context, cancellationToken).ConfigureAwait(false) is Error since)
        {
            await work.RollbackAsync().ConfigureAwait(false);

            return Result.Failure(since);
        }

        held = await directory.HeldAsync(subject, cancellationToken).ConfigureAwait(false);

        if (Named(held, kind, choice, named) is Error moved)
        {
            await work.RollbackAsync().ConfigureAwait(false);

            return Result.Failure(moved);
        }

        await directory
            .SettleBackupAsync(subject, kind, choice, named, cancellationToken)
            .ConfigureAwait(false);

        _ = await TellAsync(
                held.NoticeSet,
                subject,
                MessageKind.IdentifierSettingsChanged,
                source,
                link: null,
                cancellationToken)
            .ConfigureAwait(false);

        if ((await work.CommitAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(() => null, error => error) is Error notCommitted)
        {
            return Result.Failure(notCommitted);
        }

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

        // IDN-ACCT-007 AC2: a restricted account changes none of its settings.
        if (await restriction.RefusedAsync(context, cancellationToken).ConfigureAwait(false)
            is Error restricted)
        {
            return Result.Failure(restricted);
        }

        HeldIdentifiers held = await directory.HeldAsync(subject, cancellationToken)
            .ConfigureAwait(false);

        if (await UnremovableAsync(held, identifier, cancellationToken).ConfigureAwait(false) is Error refused)
        {
            return Result.Failure(refused);
        }

        Error? failure = null;

        TimeSpan window = (await configuration
                .ReadAsync(Settings.IdentifierChangeCoolingOff, cancellationToken).ConfigureAwait(false))
            .Match(read => read, error => Withheld<TimeSpan>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure(failure);
        }

        // D-178: the step-up is judged after every other refusal the removal can give
        // before its transaction.
        if (await stepUp
                .PassedAsync(subject, session, StepUpAction.IdentifierRemove, cancellationToken)
                .ConfigureAwait(false)
            is Error closed)
        {
            return Result.Failure(closed);
        }

        DateTimeOffset now = time.GetUtcNow();

        if ((await work.BeginAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(() => null, error => error) is Error notBegun)
        {
            return Result.Failure(notBegun);
        }

        // D-166 X3: as for a promotion.
        await directory.HoldAsync(subject, cancellationToken).ConfigureAwait(false);

        // AUTHZ-GATE-006, D-183: the restriction is asked again under that lock, which the
        // operation takes itself before any other, so one committed since the gate step
        // refuses the removal before anything is written.
        if (await restriction.RefusedAsync(context, cancellationToken).ConfigureAwait(false) is Error since)
        {
            await work.RollbackAsync().ConfigureAwait(false);

            return Result.Failure(since);
        }

        held = await directory.HeldAsync(subject, cancellationToken).ConfigureAwait(false);

        if (await UnremovableAsync(held, identifier, cancellationToken).ConfigureAwait(false) is Error moved)
        {
            await work.RollbackAsync().ConfigureAwait(false);

            return Result.Failure(moved);
        }

        HeldIdentifier going = held.Find(identifier)!;

        // REG-IDENT-006, D-187: a pending add the account lists is no identifier to
        // remove. Naming it ends its pending verification as an abandon does, with no
        // undo, reservation, notice, session ended or event.
        if (going.IsPending)
        {
            // Its row is locked before its records, as a code's presentation locks them.
            _ = await pending.FindForUpdateAsync(identifier, cancellationToken).ConfigureAwait(false);

            await EndRecordsAsync(identifier, cancellationToken).ConfigureAwait(false);
            await pending.RemoveAsync(identifier, cancellationToken).ConfigureAwait(false);

            return (await work.CommitAsync(cancellationToken).ConfigureAwait(false))
                .Match(Result.Success, Result.Failure);
        }

        // REG-IDENT-006: the removal reserves the value under its lock, so an add of it
        // presented meanwhile finds it held or reserved, never free.
        await directory.LockValuesAsync([(going.Kind, going.Canonical)], cancellationToken).ConfigureAwait(false);

        await EndRecordsAsync(identifier, cancellationToken).ConfigureAwait(false);
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

        Result published = await events
            .PublishAsync(
                new IdentifierRemoved(now, Key(identifier, now), identifier, going.Kind)
                {
                    Subject = subject,
                },
                cancellationToken)
            .ConfigureAwait(false);

        if (published.Match(() => (Error?)null, error => error) is Error unpublished)
        {
            await work.RollbackAsync().ConfigureAwait(false);

            return Result.Failure(unpublished);
        }

        if ((await work.CommitAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(() => null, error => error) is Error notCommitted)
        {
            return Result.Failure(notCommitted);
        }

        return Result.Success();
    }

    /// <inheritdoc/>
    public async ValueTask<Result> UndoAsync(
        [NeverLogged] string linkToken,
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

        if (Overfull(
                await directory.HeldAsync(given.Subject, cancellationToken).ConfigureAwait(false),
                given,
                maximum))
        {
            return Result.Failure(Error.From(ErrorCodes.IdentifierMaximum));
        }

        if ((await work.BeginAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(() => null, error => error) is Error notBegun)
        {
            return Result.Failure(notBegun);
        }

        // D-166 X3: the removal is read again under the set's lock, so of two undos at
        // once the second finds the value already back and answers as for a spent link.
        await directory.HoldAsync(given.Subject, cancellationToken).ConfigureAwait(false);

        // REG-IDENT-006: the undo writes the value back under its lock. A write of the
        // value to the account meanwhile ended the reservation, and the link then
        // answers as one past its window.
        await directory.LockValuesAsync([(given.Kind, given.Canonical)], cancellationToken).ConfigureAwait(false);

        if (await directory
                .GivenUpAsync(OpaqueToken.Of(linkToken).Fingerprint(), cancellationToken)
                .ConfigureAwait(false) is null)
        {
            await work.RollbackAsync().ConfigureAwait(false);

            return Result.Failure(Error.From(ErrorCodes.ChangeWindowElapsed));
        }

        // REG-IDENT-006 (D-188): the maximum is judged again on the set under its lock,
        // against the verified identifiers alone, so an add verified since refuses the
        // undo and a pending add never does.
        if (Overfull(
                await directory.HeldAsync(given.Subject, cancellationToken).ConfigureAwait(false),
                given,
                maximum))
        {
            await work.RollbackAsync().ConfigureAwait(false);

            return Result.Failure(Error.From(ErrorCodes.IdentifierMaximum));
        }

        // REG-IDENT-004: an add of the value the account staged meanwhile is left as it
        // stands. The undo's write is what its verification then finds, so it writes
        // nothing and the add is the sweep's.
        await directory.TakeBackAsync(given.Id, cancellationToken).ConfigureAwait(false);

        HeldIdentifiers held = await directory.HeldAsync(given.Subject, cancellationToken)
            .ConfigureAwait(false);

        _ = await TellAsync(
                held.NoticeSet,
                given.Subject,
                MessageKind.IdentifierAdded,
                source,
                link: null,
                cancellationToken)
            .ConfigureAwait(false);

        Result published = await events
            .PublishAsync(
                new IdentifierAdded(now, Key(given.Id, now), given.Id, given.Kind)
                {
                    Subject = given.Subject,
                },
                cancellationToken)
            .ConfigureAwait(false);

        if (published.Match(() => (Error?)null, error => error) is Error unpublished)
        {
            await work.RollbackAsync().ConfigureAwait(false);

            return Result.Failure(unpublished);
        }

        if ((await work.CommitAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(() => null, error => error) is Error notCommitted)
        {
            return Result.Failure(notCommitted);
        }

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

        if (StepUpGuard.RefusedInBreakGlass(context, StepUpAction.IdentifierAdd) is Error withheld)
        {
            return Result.Failure(withheld);
        }

        // IDN-ACCT-007 AC2: a restricted account changes none of its settings.
        if (await restriction.RefusedAsync(context, cancellationToken).ConfigureAwait(false)
            is Error restricted)
        {
            return Result.Failure(restricted);
        }

        if (await stepUp
                .PassedAsync(subject, session, StepUpAction.IdentifierAdd, cancellationToken)
                .ConfigureAwait(false)
            is Error closed)
        {
            return Result.Failure(closed);
        }

        return await StagedAsync(context, subject, session, identifier, value, source, cancellationToken)
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
                    asking: null,
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
        AccessContext? asking,
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

        if (changing.IsLocked || changing.IsPersonal)
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

        if ((await work.BeginAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(() => null, error => error) is Error notBegun)
        {
            return Result.Failure(notBegun);
        }

        // AUTHZ-GATE-006, D-183: the gate is asked again inside the unit of work, with the
        // acting account's row held before any other lock, so a restriction committed since
        // the gate step refuses the replacement before anything is written.
        if (await RestrictedSinceAsync(asking, cancellationToken).ConfigureAwait(false) is Error since)
        {
            await work.RollbackAsync().ConfigureAwait(false);

            return Result.Failure(since);
        }

        // REG-SESS-005, CONV-DESIGN-003: whether the new value is held or reserved is
        // judged under its lock, taken before the code's send is counted.
        await directory.LockValuesAsync([(changing.Kind, canonical)], cancellationToken).ConfigureAwait(false);

        // CONV-DESIGN-003: a replace by a value the account holds already writes nothing,
        // so its unit of work is rolled back and answered as any other.
        if (await directory.OwnerAsync(changing.Kind, canonical, cancellationToken).ConfigureAwait(false)
            == subject)
        {
            await work.RollbackAsync().ConfigureAwait(false);

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

        // REG-SESS-005, AUTH-ABUSE-004: a held or reserved value is staged as a fresh
        // one is, and a send the restrictions refuse stages nothing either way.
        if (await AskAsync(waiting, source, cancellationToken).ConfigureAwait(false)
            is Error refused)
        {
            await work.RollbackAsync().ConfigureAwait(false);

            return Result.Failure(refused);
        }

        if (waiting.OldMustConfirm
            && await AskOldAsync(waiting, changing, source, cancellationToken).ConfigureAwait(false)
                is Error asked)
        {
            await work.RollbackAsync().ConfigureAwait(false);

            return Result.Failure(asked);
        }

        await pending.RecordAsync(waiting, cancellationToken).ConfigureAwait(false);

        if ((await work.CommitAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(() => null, error => error) is Error notCommitted)
        {
            return Result.Failure(notCommitted);
        }

        return Result.Success();
    }

    // AUTHZ-GATE-006, D-183: the restriction asked again inside the unit of work, where
    // the gate judges it with the account's row held. An enrolment session is not asked
    // about the restriction, there as at the gate step (IDN-ACCT-007).
    private async ValueTask<Error?> RestrictedSinceAsync(AccessContext? asking, CancellationToken cancellationToken) =>
        asking is null
            ? null
            : await restriction.RefusedAsync(asking, cancellationToken).ConfigureAwait(false);

    // REG-IDENT-005: only an identifier the account holds and has proved is promoted,
    // and the personal email stays non-primary for the whole membership (REG-MAIL-001).
    private static Error? Unpromotable(HeldIdentifier? promoted) =>
        promoted is null ? Error.From(ErrorCodes.IdentifierInvalid)
        : !promoted.IsVerified ? Error.From(ErrorCodes.IdentifierUnverified)
        : promoted.IsPersonal ? Error.From(ErrorCodes.IdentifierLocked)
        : null;

    private static IntegerSetting Maximum(IdentifierKind kind) =>
        kind is IdentifierKind.Email ? Settings.IdentifiersEmailMax : Settings.IdentifiersPhoneMax;

    private static string Key(IdentifierId identifier, DateTimeOffset at) =>
        string.Create(CultureInfo.InvariantCulture, $"{identifier.Value}@{at.UtcTicks}");

    // REG-IDENT-006 (D-188): an undo counts the account's verified identifiers of the
    // kind alone, so a pending add or an unverified identifier never refuses it. The
    // undo of a replace moves the value back onto the identifier that stands and adds
    // none to the kind, so the maximum does not judge it.
    private static bool Overfull(HeldIdentifiers held, GivenUpIdentifier given, int maximum) =>
        held.Find(given.Id) is null && held.Verified(given.Kind) >= maximum;

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
            || backup.Kind != kind)
        {
            return Error.From(ErrorCodes.IdentifierInvalid);
        }

        if (!backup.IsVerified)
        {
            return Error.From(ErrorCodes.IdentifierUnverified);
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
        [NeverLogged] string linkToken,
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

    // The personal email stays on the account for the whole membership (REG-MAIL-001),
    // the primary of a kind is not removed, and neither is the last way a kind reaches.
    private async ValueTask<Error?> UnremovableAsync(
        HeldIdentifiers held,
        IdentifierId identifier,
        CancellationToken cancellationToken)
    {
        if (held.Find(identifier) is not HeldIdentifier going)
        {
            return Error.From(ErrorCodes.IdentifierInvalid);
        }

        if (going.IsPersonal)
        {
            return Error.From(ErrorCodes.IdentifierLocked);
        }

        if (going.IsPrimary)
        {
            return Error.From(ErrorCodes.IdentifierPrimary);
        }

        return await RequiredAsync(going, held, cancellationToken).ConfigureAwait(false);
    }

    // IDN-ATTR-001: a message goes out in the language the account settled on, and in
    // every language the deployment declares where it has settled none; the operations
    // here carry no locale of the person's request.
    private async ValueTask<string?> LanguageAsync(SubjectId subject, CancellationToken cancellationToken)
    {
        string? settled = await directory.LanguageAsync(subject, cancellationToken)
            .ConfigureAwait(false);

        IReadOnlyList<string> languages = (await configuration
                .ReadAsync(Settings.NotificationLanguages, cancellationToken).ConfigureAwait(false))
            .Match(read => read, error => throw new InvalidOperationException(error.Code.ToString()));

        return RecipientLanguage.Of(settled, requested: null, languages);
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

    // Whether a value is out of the asking account's reach: an account holds it, or an
    // undo reserves it to another account (REG-SESS-005, REG-IDENT-006). Another
    // account that holds it is told, once in a window; a reservation tells nobody. A
    // value reserved to the account asking is free to it. The caller holds the value's
    // lock.
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
            || (await directory
                    .ReservedToAsync(kind, canonical, time.GetUtcNow(), cancellationToken)
                    .ConfigureAwait(false) is SubjectId reserved
                && reserved != subject);
    }

    // REG-IDENT-004, REG-IDENT-007, REG-SESS-005: the judgement made again where a
    // verified value would be written. An identifier has come to hold the value, the
    // account's own included, or an undo to reserve it to another account. The caller
    // holds the value's lock.
    private async ValueTask<bool> HeldSinceAsync(
        SubjectId subject,
        IdentifierKind kind,
        string canonical,
        DateTimeOffset now,
        CancellationToken cancellationToken) =>
        await directory.OwnerAsync(kind, canonical, cancellationToken).ConfigureAwait(false) is not null
        || (await directory.ReservedToAsync(kind, canonical, now, cancellationToken).ConfigureAwait(false)
                is SubjectId reserved
            && reserved != subject);

    // REG-IDENT-004 (D-188): the maximum judged again where an add's verified value
    // would be written, against the account's verified identifiers of the kind alone:
    // pending adds are not counted, so two of them do not refuse each other. The caller
    // holds the set's lock and the value's.
    private async ValueTask<Error?> FullAsync(
        SubjectId subject,
        IdentifierKind kind,
        CancellationToken cancellationToken)
    {
        Error? failure = null;

        int maximum = (await configuration
                .ReadAsync(Maximum(kind), cancellationToken).ConfigureAwait(false))
            .Match(read => read, error => Withheld<int>(error, ref failure));

        if (failure is not null)
        {
            return failure;
        }

        HeldIdentifiers held = await directory.HeldAsync(subject, cancellationToken)
            .ConfigureAwait(false);

        return held.Verified(kind) >= maximum ? Error.From(ErrorCodes.IdentifierMaximum) : null;
    }

    // REG-IDENT-004 (D-187): every add is staged as a pending verification that holds
    // the value, a held or reserved value as a fresh one, and no identifier is written
    // until it verifies. What comes back beside a refusal is whether anything was
    // staged or sent, which is false only for a value the account holds already.
    private async ValueTask<(Error? Refused, bool Staged)> StageAsync(
        SubjectId subject,
        SessionId session,
        HeldIdentifiers held,
        IdentifierKind kind,
        string entered,
        string canonical,
        string source,
        CancellationToken cancellationToken)
    {
        // A value the account already lists as a pending add is the one it is waiting
        // on, so asking again sends again rather than starting a second wait.
        if (Standing(held, canonical) is HeldIdentifier standing)
        {
            PendingVerification? again = standing.IsVerified
                ? null
                : await pending.FindForUpdateAsync(standing.Id, cancellationToken).ConfigureAwait(false);

            if (again is null)
            {
                return (null, false);
            }

            if (await AskAsync(again, source, cancellationToken).ConfigureAwait(false) is Error refused)
            {
                return (refused, false);
            }

            await pending.RecordAsync(again, cancellationToken).ConfigureAwait(false);

            return (null, true);
        }

        var staged = PendingVerification.ToAdd(
            subject,
            session,
            StagedIdentity.Of(IdentifierId.New(time), kind, entered, canonical),
            time.GetUtcNow());

        await pending.AddAsync(staged, cancellationToken).ConfigureAwait(false);

        if (await AskAsync(staged, source, cancellationToken).ConfigureAwait(false) is Error unasked)
        {
            return (unasked, false);
        }

        await pending.RecordAsync(staged, cancellationToken).ConfigureAwait(false);

        // REG-IDENT-004 AC3, API-CONV-005: the set as it stands hears of every addition,
        // once, of a held or reserved value as of a fresh one.
        _ = await TellAsync(
                held.NoticeSet,
                subject,
                MessageKind.IdentifierAdded,
                source,
                link: null,
                cancellationToken)
            .ConfigureAwait(false);

        return (null, true);
    }

    // REG-SESS-005, API-CONV-005: the fresh case and the case where the value is out of
    // reach are one path. A fresh value is sent its code and its link; a held or
    // reserved one is given a record no code answers, and nothing is sent to it. Each
    // is judged at every ask, so a value freed since is sent a code. The caller holds
    // the value's lock.
    private async ValueTask<Error?> AskAsync(
        PendingVerification waiting,
        string source,
        CancellationToken cancellationToken)
    {
        StagedIdentity staged = waiting.Staged;

        return await TakenAsync(waiting.Subject, staged.Kind, staged.Canonical, source, cancellationToken)
            .ConfigureAwait(false)
            ? await WithholdAsync(waiting, source, cancellationToken).ConfigureAwait(false)
            : await SendCodeAsync(waiting, source, cancellationToken).ConfigureAwait(false);
    }

    // AUTH-FACT-004, AUTH-ABUSE-004: an ask of a code for a held or reserved value is
    // judged and counted against the restrictions as its message would be, and refused
    // by them alike; admitted, the pending verification is given a verification-code
    // record that lives and counts as a sent code's does and that no code matches.
    // Nothing is sent, and no link stands for a press to prove.
    private async ValueTask<Error?> WithholdAsync(
        PendingVerification waiting,
        string source,
        CancellationToken cancellationToken)
    {
        StagedIdentity staged = waiting.Staged;

        Result drawn = await restrictions
            .DrawAsync(
                new OutboundMessage(
                    Destination(staged.Kind, staged.Canonical),
                    MessageKind.VerificationLink,
                    RestrictionPurpose.Verification,
                    source,
                    await LanguageAsync(waiting.Subject, cancellationToken).ConfigureAwait(false))
                {
                    Subject = waiting.Subject,
                },
                cancellationToken)
            .ConfigureAwait(false);

        if (drawn.Match(() => (Error?)null, error => error) is Error refused)
        {
            return refused;
        }

        staged.Unlinked();

        return (await codes
                .WithholdAsync(PendingVerification.CodeHolder(staged.Id), cancellationToken)
                .ConfigureAwait(false))
            .Match(() => (Error?)null, error => error);
    }

    private async ValueTask<Error?> SendCodeAsync(
        PendingVerification waiting,
        string source,
        CancellationToken cancellationToken)
    {
        Error? failure = null;

        StagedIdentity staged = waiting.Staged;

        // AUTH-ABUSE-004: the code is issued and its message undertaken in the caller's
        // unit of work, so a send the restrictions refuse leaves no code behind it.
        string code = (await codes.IssueAsync(PendingVerification.CodeHolder(staged.Id), cancellationToken).ConfigureAwait(false))
            .Match(drawn => drawn, error => Withheld<string>(error, ref failure));

        if (failure is not null)
        {
            return failure;
        }

        var link = OpaqueToken.Draw(randomness);

        Result<SendReference> sent = await sending
            .UndertakeAsync(
                new OutboundMessage(
                    Destination(staged.Kind, staged.Canonical),
                    MessageKind.VerificationLink,
                    RestrictionPurpose.Verification,
                    source,
                    await LanguageAsync(waiting.Subject, cancellationToken).ConfigureAwait(false))
                {
                    Subject = waiting.Subject,
                    Values = new Dictionary<string, string>(capacity: 2, StringComparer.Ordinal)
                    {
                        ["code"] = code,
                        ["link"] = landing.Of(LinkKind.Identifier, link.Value),
                    },
                },
                cancellationToken)
            .ConfigureAwait(false);

        if (sent.Match(_ => (Error?)null, error => error) is Error refused)
        {
            return refused;
        }

        staged.Linked(link.Fingerprint());

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
            .UndertakeAsync(
                new OutboundMessage(
                    Destination(displaced.Kind, displaced.Canonical),
                    MessageKind.IdentifierChangeConfirm,
                    RestrictionPurpose.Verification,
                    source,
                    await LanguageAsync(waiting.Subject, cancellationToken).ConfigureAwait(false))
                {
                    Subject = waiting.Subject,
                    Values = new Dictionary<string, string>(capacity: 1, StringComparer.Ordinal)
                    {
                        ["link"] = landing.Of(LinkKind.IdentifierConfirm, link.Value),
                    },
                },
                cancellationToken)
            .ConfigureAwait(false);

        if (sent.Match(_ => (Error?)null, error => error) is Error refused)
        {
            return refused;
        }

        // REG-IDENT-007, D-187: the confirmation lives code.verification.lifetime from
        // its send, in a record of its own that holds no code.
        Result held = await codes
            .WithholdAsync(PendingVerification.ConfirmationHolder(waiting.Identifier), cancellationToken)
            .ConfigureAwait(false);

        if (held.Match(() => (Error?)null, error => error) is Error unheld)
        {
            return unheld;
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

        if (failure is not null)
        {
            return 0;
        }

        // D-166 X3: the holder is told once however many ask at once.
        await notices.HoldAsync(canonical, cancellationToken).ConfigureAwait(false);

        if (!await notices
            .FirstAsync(canonical, time.GetUtcNow(), window, cancellationToken)
            .ConfigureAwait(false))
        {
            return 0;
        }

        // A refusal to tell the holder is not a refusal of the operation: the caller
        // sees the same outcome either way (REG-SESS-005 AC1).
        Result<SendReference> sent = await sending
            .UndertakeAsync(
                new OutboundMessage(
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
        [NeverLogged] string? link,
        CancellationToken cancellationToken)
    {
        if (reached.Count is 0)
        {
            return 0;
        }

        string? language = await LanguageAsync(subject, cancellationToken).ConfigureAwait(false);
        int told = 0;

        foreach (HeldIdentifier identifier in reached)
        {
            var request = new OutboundMessage(
                Destination(identifier.Kind, identifier.Canonical),
                message,
                RestrictionPurpose.Notification,
                source,
                language)
            {
                Subject = subject,
                Values = link is null
                    ? Nothing
                    : new Dictionary<string, string>(capacity: 1, StringComparer.Ordinal)
                    {
                        ["link"] = link,
                    },
            };

            // A security notice one destination refuses still reaches the rest: the
            // set exists so that no one channel can silence it.
            Result<SendReference> sent = await sending
                .UndertakeAsync(request, cancellationToken)
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
                landing.Of(LinkKind.Undo, undo.Value),
                cancellationToken)
            .ConfigureAwait(false);

        _ = await TellAsync(
                [going],
                subject,
                MessageKind.IdentifierDetached,
                source,
                link: null,
                cancellationToken)
            .ConfigureAwait(false);
    }

    // AUTH-FACT-004, REG-IDENT-007: a pending verification ended leaves neither of the
    // records it holds, the code's and, for a replace the old address was asked to
    // confirm, the confirmation's.
    private async ValueTask EndRecordsAsync(IdentifierId identifier, CancellationToken cancellationToken)
    {
        await codes.EndAsync(PendingVerification.CodeHolder(identifier), cancellationToken).ConfigureAwait(false);
        await codes
            .EndAsync(PendingVerification.ConfirmationHolder(identifier), cancellationToken)
            .ConfigureAwait(false);
    }

    // IDN-LIFE-008: the session that made the change is kept, where there is one; a
    // change made from no browser keeps none.
    private async ValueTask EndOthersAsync(
        SubjectId subject,
        SessionId? keeping,
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

    // What a completed verification does to the account: an add writes its identifier,
    // verified, under the pending verification's identifier; a replace swaps the value
    // of the one it named, keeps the session it completed under and holds the displaced
    // value for the undo. Either writes nothing and is answered auth.code.expired where
    // the value has come to be held or reserved since it was staged, and an add writes
    // nothing and is answered identity.identifier.maximum where the account's verified
    // identifiers already fill the kind; its caller then rolls the presentation back
    // (REG-IDENT-004, REG-IDENT-007).
    private async ValueTask<Result> SettleAsync(
        PendingVerification waiting,
        SessionId? completing,
        DateTimeOffset now,
        string source,
        CancellationToken cancellationToken)
    {
        if (!waiting.IsSettled)
        {
            await pending.RecordAsync(waiting, cancellationToken).ConfigureAwait(false);

            return Result.Success();
        }

        StagedIdentity staged = waiting.Staged;

        if (waiting.IsReplacement)
        {
            if ((await SwapAsync(waiting, completing, now, source, cancellationToken).ConfigureAwait(false))
                .Match(() => (Error?)null, error => error) is Error unswapped)
            {
                return Result.Failure(unswapped);
            }
        }
        else
        {
            // REG-SESS-005: an add's verification judges and writes under the value's
            // lock.
            await directory.LockValuesAsync([(staged.Kind, staged.Canonical)], cancellationToken).ConfigureAwait(false);

            if (await HeldSinceAsync(waiting.Subject, staged.Kind, staged.Canonical, now, cancellationToken)
                .ConfigureAwait(false))
            {
                return Result.Failure(Error.From(ErrorCodes.CodeExpired));
            }

            if (await FullAsync(waiting.Subject, staged.Kind, cancellationToken).ConfigureAwait(false)
                is Error full)
            {
                return Result.Failure(full);
            }

            await directory
                .TakeOnAsync(
                    waiting.Subject,
                    staged.Id,
                    staged.Kind,
                    staged.Entered,
                    staged.Canonical,
                    now,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        await pending.RemoveAsync(staged.Id, cancellationToken).ConfigureAwait(false);

        Result published = await events
            .PublishAsync(
                new IdentifierAdded(now, Key(staged.Id, now), staged.Id, staged.Kind)
                {
                    Subject = waiting.Subject,
                },
                cancellationToken)
            .ConfigureAwait(false);

        if (published.Match(() => (Error?)null, error => error) is Error unpublished)
        {
            return Result.Failure(unpublished);
        }

        return Result.Success();
    }

    private async ValueTask<Result> SwapAsync(
        PendingVerification waiting,
        SessionId? completing,
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

        if (failure is not null)
        {
            return Result.Failure(failure);
        }

        if (held.Find(staged.Id) is not HeldIdentifier displaced)
        {
            return Result.Success();
        }

        // REG-IDENT-007: the swap writes the new value to the account and reserves the
        // displaced one, each under its lock, both taken in one order.
        await directory
            .LockValuesAsync(
                [(staged.Kind, staged.Canonical), (displaced.Kind, displaced.Canonical)],
                cancellationToken)
            .ConfigureAwait(false);

        // REG-IDENT-007: whether the new value is held or reserved is judged again
        // under its lock, before the swap's first write.
        if (await HeldSinceAsync(waiting.Subject, staged.Kind, staged.Canonical, now, cancellationToken)
            .ConfigureAwait(false))
        {
            return Result.Failure(Error.From(ErrorCodes.CodeExpired));
        }

        var undo = OpaqueToken.Draw(randomness);

        // IDN-LIFE-008 AC1: the value that signed in is gone, so every session but the
        // one the change completes under ends with it, the one that staged it included.
        await EndOthersAsync(waiting.Subject, completing, now, cancellationToken)
            .ConfigureAwait(false);

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
                landing.Of(LinkKind.Undo, undo.Value),
                cancellationToken)
            .ConfigureAwait(false);

        return Result.Success();
    }
}
