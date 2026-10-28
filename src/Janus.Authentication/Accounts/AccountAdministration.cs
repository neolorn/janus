using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.BreakGlass;
using Janus.Authentication.Factors;
using Janus.Authentication.Identifiers;
using Janus.Authentication.Policies;
using Janus.Authentication.Records;
using Janus.Authentication.Sessions;
using Janus.Core;
using Janus.Core.Configuration;

namespace Janus.Authentication.Accounts;

/// <summary>
/// What an administrator does to the standing of someone else's account: suspends it,
/// reactivates it, lifts its restriction and cancels its deletion, and reads the photo it
/// shows; what a host reads of another person's account, and the preferences an
/// administrator sets for it.
/// </summary>
/// <param name="scope">Whether the caller administers accounts in the deployment.</param>
/// <param name="stepUp">What the gated operations ask of the administrator's session.</param>
/// <param name="directory">Where the standing is read and the transition carried.</param>
/// <param name="emergency">Which account no administrator suspends.</param>
/// <param name="sessions">What a suspension ends.</param>
/// <param name="links">Where the link a deletion notice carried is held.</param>
/// <param name="configuration">Where the grace window and the field switches are read.</param>
/// <param name="events">Where the transition is announced.</param>
/// <param name="audit">Where what the administrator did is recorded.</param>
/// <param name="photos">Where the photo an account shows is read.</param>
/// <param name="reach">Where a read is asked, and which accounts it reaches.</param>
/// <param name="identifiers">Where an account's identifiers are read and found.</param>
/// <param name="records">Where a person record's contact is read.</param>
/// <param name="memberships">Which organizations an account is a current member of.</param>
/// <param name="declarations">The preference keys the host declared.</param>
/// <param name="volume">Where the accounts a person reads are counted.</param>
/// <param name="work">The one transaction an operation runs in.</param>
/// <param name="time">The clock the deployment runs on.</param>
/// <remarks>
/// Implements IDN-LIFE-013, AUTH-SESS-010, PRIV-RIGHT-004, IDN-AUD-001, IDN-READ-003,
/// IDN-READ-004, REG-PREF-001, REC-ADM-001 and chapter 09 section 8a. The grants of a suspended
/// account are left in place, which is what lets reactivation restore them exactly; the
/// gate confers nothing on an account that is not active.
/// </remarks>
internal sealed class AccountAdministration(
    AdministrativeScope scope,
    StepUpGuard stepUp,
    IAccountDirectory directory,
    IEmergencyAccount emergency,
    ISessionStore sessions,
    ILifecycleLinkStore links,
    IConfigurationStore configuration,
    IEvents events,
    IAccountAudit audit,
    ProfilePhotos photos,
    AccountReach reach,
    IIdentifierDirectory identifiers,
    IRecordDirectory records,
    IMembershipLookup memberships,
    PreferenceDeclarations declarations,
    IReadVolume volume,
    IUnitOfWork work,
    TimeProvider time) : IAccounts
{
    /// <inheritdoc/>
    public async ValueTask<Result> SuspendAsync(
        AccessContext context,
        SessionId session,
        SubjectId subject,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Acting is not SubjectId acting)
        {
            return Result.Failure(Error.From(ErrorCodes.Denied));
        }

        if (await scope.RefusedAsync(context, Permissions.AccountManage, cancellationToken).ConfigureAwait(false)
            is Error refused)
        {
            return Result.Failure(refused);
        }

        (Error? refusal, AccountState? state) = await SuspendableAsync(subject, cancellationToken)
            .ConfigureAwait(false);

        if (refusal is not null || state is null)
        {
            return refusal is null ? Result.Success() : Result.Failure(refusal);
        }

        if (await stepUp
                .PassedAsync(acting, session, StepUpAction.AccountSuspend, cancellationToken)
                .ConfigureAwait(false)
            is Error challenged)
        {
            return Result.Failure(challenged);
        }

        DateTimeOffset now = time.GetUtcNow();

        if ((await work.BeginAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(_ => null, error => error) is Error notBegun)
        {
            return Result.Failure(notBegun);
        }

        // AUTHZ-GATE-006, D-183: the gate is asked again inside the unit of work, with the
        // acting account's row held before any other lock, so a restriction committed since
        // the gate step refuses the change before anything is written.
        if (await AskedAgainAsync(context, acting, subject, cancellationToken).ConfigureAwait(false) is Error since)
        {
            await work.RollbackAsync().ConfigureAwait(false);

            return Result.Failure(since);
        }

        // D-166 X3: decided again on the account's row under its lock, so a transition
        // committed since the first decision is the one this one follows.
        await directory.HoldAsync(subject, cancellationToken).ConfigureAwait(false);

        (refusal, state) = await SuspendableAsync(subject, cancellationToken).ConfigureAwait(false);

        if (refusal is not null)
        {
            await work.RollbackAsync().ConfigureAwait(false);

            return Result.Failure(refusal);
        }

        // An account an administrator suspended meanwhile stands as asked: the
        // operation is done with nothing to write.
        if (state is null)
        {
            // CONV-DESIGN-003: nothing was written, so the unit of work is rolled back.
            await work.RollbackAsync().ConfigureAwait(false);

            return Result.Success();
        }

        await directory.SuspendAsync(subject, cancellationToken).ConfigureAwait(false);

        // AUTH-SESS-010: the sessions end in the transaction that suspends, so the
        // first request on any of them after the commit is refused.
        if (await SessionEnds
                .AnnouncedAsync(
                    events,
                    await sessions.EndAccountAsync(subject, now, cancellationToken).ConfigureAwait(false),
                    SessionEndReason.Account,
                    now,
                    context,
                    cancellationToken)
                .ConfigureAwait(false) is Error unannounced)
        {
            await work.RollbackAsync().ConfigureAwait(false);

            return Result.Failure(unannounced);
        }

        // An account its owner deactivated is already suspended, so taking it over
        // changes who reverses it and announces nothing: the state did not change.
        if (state is not AccountState.Suspended)
        {
            Result published = await events
                .PublishAsync(
                    new AccountSuspended(now, Key(subject, now), SuspensionOrigin.Administrator)
                    {
                        Subject = subject,
                        Actor = acting,
                        Effective = context.Effective,
                    },
                    cancellationToken)
                .ConfigureAwait(false);

            if (published.Match(() => (Error?)null, error => error) is Error unpublished)
            {
                await work.RollbackAsync().ConfigureAwait(false);

                return Result.Failure(unpublished);
            }
        }

        await audit
            .AdministeredAsync(AuditActions.AccountSuspended, acting, context.BreakGlassReason, subject, now, cancellationToken)
            .ConfigureAwait(false);

        if ((await work.CommitAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(() => null, error => error) is Error notCommitted)
        {
            return Result.Failure(notCommitted);
        }

        return Result.Success();
    }

    /// <inheritdoc/>
    public async ValueTask<Result> ReactivateAsync(
        AccessContext context,
        SessionId session,
        SubjectId subject,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Acting is not SubjectId acting)
        {
            return Result.Failure(Error.From(ErrorCodes.Denied));
        }

        if (await scope.RefusedAsync(context, Permissions.AccountManage, cancellationToken).ConfigureAwait(false)
            is Error refused)
        {
            return Result.Failure(refused);
        }

        if (await ReactivationRefusedAsync(subject, cancellationToken).ConfigureAwait(false) is Error refusal)
        {
            return Result.Failure(refusal);
        }

        if (await stepUp
                .PassedAsync(acting, session, StepUpAction.AccountReactivate, cancellationToken)
                .ConfigureAwait(false)
            is Error challenged)
        {
            return Result.Failure(challenged);
        }

        DateTimeOffset now = time.GetUtcNow();

        if ((await work.BeginAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(_ => null, error => error) is Error notBegun)
        {
            return Result.Failure(notBegun);
        }

        // AUTHZ-GATE-006, D-183: the gate is asked again inside the unit of work, with the
        // acting account's row held before any other lock, so a restriction committed since
        // the gate step refuses the change before anything is written.
        if (await AskedAgainAsync(context, acting, subject, cancellationToken).ConfigureAwait(false) is Error since)
        {
            await work.RollbackAsync().ConfigureAwait(false);

            return Result.Failure(since);
        }

        // D-166 X3: as for a suspension.
        await directory.HoldAsync(subject, cancellationToken).ConfigureAwait(false);

        if (await ReactivationRefusedAsync(subject, cancellationToken).ConfigureAwait(false) is Error moved)
        {
            await work.RollbackAsync().ConfigureAwait(false);

            return Result.Failure(moved);
        }

        await directory.ReinstateAsync(subject, cancellationToken).ConfigureAwait(false);

        Result published = await events
            .PublishAsync(
                new AccountReactivated(now, Key(subject, now))
                {
                    Subject = subject,
                    Actor = acting,
                    Effective = context.Effective,
                },
                cancellationToken)
            .ConfigureAwait(false);

        if (published.Match(() => (Error?)null, error => error) is Error unpublished)
        {
            await work.RollbackAsync().ConfigureAwait(false);

            return Result.Failure(unpublished);
        }

        await audit
            .AdministeredAsync(AuditActions.AccountReactivated, acting, context.BreakGlassReason, subject, now, cancellationToken)
            .ConfigureAwait(false);

        if ((await work.CommitAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(() => null, error => error) is Error notCommitted)
        {
            return Result.Failure(notCommitted);
        }

        return Result.Success();
    }

    /// <inheritdoc/>
    public async ValueTask<Result> LiftRestrictionAsync(
        AccessContext context,
        SessionId session,
        SubjectId subject,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Acting is not SubjectId acting)
        {
            return Result.Failure(Error.From(ErrorCodes.Denied));
        }

        if (await scope.RefusedAsync(context, Permissions.AccountManage, cancellationToken).ConfigureAwait(false)
            is Error refused)
        {
            return Result.Failure(refused);
        }

        if (await LiftRefusedAsync(subject, cancellationToken).ConfigureAwait(false) is Error refusal)
        {
            return Result.Failure(refusal);
        }

        if (await stepUp
                .PassedAsync(acting, session, StepUpAction.AccountRestrictionLift, cancellationToken)
                .ConfigureAwait(false)
            is Error challenged)
        {
            return Result.Failure(challenged);
        }

        DateTimeOffset now = time.GetUtcNow();

        if ((await work.BeginAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(_ => null, error => error) is Error notBegun)
        {
            return Result.Failure(notBegun);
        }

        // AUTHZ-GATE-006, D-183: the gate is asked again inside the unit of work, with the
        // acting account's row held before any other lock, so a restriction committed since
        // the gate step refuses the change before anything is written.
        if (await AskedAgainAsync(context, acting, subject, cancellationToken).ConfigureAwait(false) is Error since)
        {
            await work.RollbackAsync().ConfigureAwait(false);

            return Result.Failure(since);
        }

        // D-166 X3: as for a suspension.
        await directory.HoldAsync(subject, cancellationToken).ConfigureAwait(false);

        if (await LiftRefusedAsync(subject, cancellationToken).ConfigureAwait(false) is Error moved)
        {
            await work.RollbackAsync().ConfigureAwait(false);

            return Result.Failure(moved);
        }

        await directory.LiftRestrictionAsync(subject, now, cancellationToken).ConfigureAwait(false);
        await audit
            .AdministeredAsync(AuditActions.RestrictionLifted, acting, context.BreakGlassReason, subject, now, cancellationToken)
            .ConfigureAwait(false);

        if ((await work.CommitAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(() => null, error => error) is Error notCommitted)
        {
            return Result.Failure(notCommitted);
        }

        return Result.Success();
    }

    /// <inheritdoc/>
    public async ValueTask<Result> CancelDeletionAsync(
        AccessContext context,
        SessionId session,
        SubjectId subject,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Acting is not SubjectId acting)
        {
            return Result.Failure(Error.From(ErrorCodes.Denied));
        }

        if (await scope.RefusedAsync(context, Permissions.AccountManage, cancellationToken).ConfigureAwait(false)
            is Error refused)
        {
            return Result.Failure(refused);
        }

        DateTimeOffset now = time.GetUtcNow();

        (Error? refusal, HeldDeletion? deleting) = await CancellableAsync(subject, now, cancellationToken)
            .ConfigureAwait(false);

        if (refusal is not null)
        {
            return Result.Failure(refusal);
        }

        if (await stepUp
                .PassedAsync(acting, session, StepUpAction.AccountDeletionCancel, cancellationToken)
                .ConfigureAwait(false)
            is Error challenged)
        {
            return Result.Failure(challenged);
        }

        if ((await work.BeginAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(_ => null, error => error) is Error notBegun)
        {
            return Result.Failure(notBegun);
        }

        // AUTHZ-GATE-006, D-183: the gate is asked again inside the unit of work, with the
        // acting account's row held before any other lock, so a restriction committed since
        // the gate step refuses the change before anything is written.
        if (await AskedAgainAsync(context, acting, subject, cancellationToken).ConfigureAwait(false) is Error since)
        {
            await work.RollbackAsync().ConfigureAwait(false);

            return Result.Failure(since);
        }

        // D-166 X3: as for a suspension.
        await directory.HoldAsync(subject, cancellationToken).ConfigureAwait(false);

        (refusal, deleting) = await CancellableAsync(subject, now, cancellationToken).ConfigureAwait(false);

        if (refusal is not null)
        {
            await work.RollbackAsync().ConfigureAwait(false);

            return Result.Failure(refusal);
        }

        if (deleting is null)
        {
            // CONV-DESIGN-003: nothing was written, so the unit of work is rolled back.
            await work.RollbackAsync().ConfigureAwait(false);

            return Result.Success();
        }

        // IDN-LIFE-003: the cancellation of a window an out-of-band request began is
        // recorded against that request.
        PrivacyRequestId? request = deleting.By is DeletionOrigin.OutOfBandRequest
            ? await directory.ErasureRequestAsync(subject, deleting.Since, cancellationToken).ConfigureAwait(false)
            : null;

        await directory.CancelDeletionAsync(subject, now, cancellationToken).ConfigureAwait(false);
        await links.RemoveAsync(subject, cancellationToken).ConfigureAwait(false);

        Result published = await events
            .PublishAsync(
                new AccountDeletionCancelled(now, Key(subject, now))
                {
                    Subject = subject,
                    Actor = acting,
                    Effective = context.Effective,
                },
                cancellationToken)
            .ConfigureAwait(false);

        if (published.Match(() => (Error?)null, error => error) is Error unpublished)
        {
            await work.RollbackAsync().ConfigureAwait(false);

            return Result.Failure(unpublished);
        }

        await audit
            .CancelledOnBehalfAsync(acting, context.BreakGlassReason, subject, request, now, cancellationToken)
            .ConfigureAwait(false);

        if ((await work.CommitAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(() => null, error => error) is Error notCommitted)
        {
            return Result.Failure(notCommitted);
        }

        return Result.Success();
    }

    /// <inheritdoc/>
    public async ValueTask<Result<ReadOnlyMemory<byte>>> ReadPhotoAsync(
        AccessContext context,
        SubjectId subject,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (await scope.RefusedAsync(context, Permissions.AccountManage, cancellationToken).ConfigureAwait(false)
            is Error refused)
        {
            return Result.Failure<ReadOnlyMemory<byte>>(refused);
        }

        return await directory.StateAsync(subject, cancellationToken).ConfigureAwait(false) is null
            ? Result.Failure<ReadOnlyMemory<byte>>(Error.From(ErrorCodes.AccountNotFound))
            : await photos.ReadOfAsync(subject, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<Result<AccountView>> ReadAsync(
        AccessContext context,
        SubjectId subject,
        OrganizationId? organization,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        Result<AccountState> reached = await ReachedAsync(context, Permissions.AccountRead, subject, organization, cancellationToken)
            .ConfigureAwait(false);

        if (reached.Match(_ => (Error?)null, error => error) is Error refused)
        {
            return Result.Failure<AccountView>(refused);
        }

        AccountState state = reached.Match(read => read, _ => default);

        bool legalName = (await configuration
                .ReadAsync(Settings.ProfileLegalName, cancellationToken).ConfigureAwait(false))
            .Match(read => read, error => throw new InvalidOperationException(error.Code.ToString()))
            is not AttributeRequirement.Off;

        bool usernames = (await configuration
                .ReadAsync(Settings.IdentifiersUsernameEnabled, cancellationToken).ConfigureAwait(false))
            .Match(read => read, error => throw new InvalidOperationException(error.Code.ToString()));

        HeldIdentifiers held = await identifiers.HeldAsync(subject, cancellationToken).ConfigureAwait(false);
        HeldProfile profile = await directory.ProfileAsync(subject, cancellationToken).ConfigureAwait(false);
        HeldPreferences preferences = await directory.PreferencesAsync(subject, cancellationToken)
            .ConfigureAwait(false);
        DateTimeOffset createdAt = await directory.CreatedAtAsync(subject, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException(ErrorCodes.AccountNotFound.ToString());
        IReadOnlyList<OrganizationId> member = await memberships.OfAsync(subject, cancellationToken)
            .ConfigureAwait(false);

        string? username = null;

        if (usernames)
        {
            foreach (HeldIdentifier identifier in held.OfKind(IdentifierKind.Username))
            {
                username = identifier.Entered;
            }
        }

        var account = new AccountView(
            subject,
            state,
            createdAt,
            new AccountViewProfile(profile.DisplayName?.Value, legalName ? profile.LegalName?.Value : null),
            new AccountViewIdentifiers(
                Shown(held, IdentifierKind.Email),
                Shown(held, IdentifierKind.Phone),
                username),
            PreferencesShown.Of(preferences, declarations),
            member);

        return await CountedAsync(context, account, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<Result<SubjectId>> FindAsync(
        AccessContext context,
        string identifier,
        OrganizationId? organization,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(identifier);

        Result<AdmittedRead> asked = await reach
            .AskedAsync(context, Permissions.AccountRead, organization, cancellationToken)
            .ConfigureAwait(false);

        if (asked.Match(_ => (Error?)null, error => error) is Error refused)
        {
            return Result.Failure<SubjectId>(refused);
        }

        bool usernames = (await configuration
                .ReadAsync(Settings.IdentifiersUsernameEnabled, cancellationToken).ConfigureAwait(false))
            .Match(read => read, error => throw new InvalidOperationException(error.Code.ToString()));

        // IDN-ACCT-004: read in the canonical form sign-in reads it in, so another case or
        // another Unicode form of the same identifier finds the same account.
        if (EnteredIdentifiers.Canonical(identifier.Trim(), usernames) is not (IdentifierKind kind, string canonical)
            || await identifiers.OwnerAsync(kind, canonical, cancellationToken).ConfigureAwait(false)
                is not SubjectId subject)
        {
            return Result.Failure<SubjectId>(Error.From(ErrorCodes.AccountNotFound));
        }

        return (await asked
                .Match(
                    read => WithinAsync(read, subject, cancellationToken),
                    error => ValueTask.FromResult(Result.Failure<AccountState>(error)))
                .ConfigureAwait(false))
            .Match(_ => Result.Success(subject), Result.Failure<SubjectId>);
    }

    /// <inheritdoc/>
    public async ValueTask<Result<ContactDetail>> ContactAsync(
        AccessContext context,
        SubjectId subject,
        OrganizationId? organization,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        // REC-ADM-001: a person record's subject is answered as an account's is, the
        // answer naming its kind.
        if (await records.HoldAsync(subject, cancellationToken).ConfigureAwait(false) is RecordStanding standing)
        {
            return await RecordContactAsync(context, subject, standing, organization, cancellationToken)
                .ConfigureAwait(false);
        }

        Result<AccountState> reached = await ReachedAsync(context, Permissions.ContactRead, subject, organization, cancellationToken)
            .ConfigureAwait(false);

        if (reached.Match(_ => (Error?)null, error => error) is Error refused)
        {
            return Result.Failure<ContactDetail>(refused);
        }

        AccountState state = reached.Match(read => read, _ => default);

        HeldIdentifiers held = await identifiers.HeldAsync(subject, cancellationToken).ConfigureAwait(false);
        HeldProfile profile = await directory.ProfileAsync(subject, cancellationToken).ConfigureAwait(false);
        HeldPreferences preferences = await directory.PreferencesAsync(subject, cancellationToken)
            .ConfigureAwait(false);

        var contact = new ContactDetail(
            SubjectKind.Account,
            profile.DisplayName?.Value,
            Reachable(held, IdentifierKind.Email),
            Reachable(held, IdentifierKind.Phone),
            preferences.Language,
            preferences.TimeZone,
            state);

        return await CountedAsync(context, contact, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<Result> SetPreferencesAsync(
        AccessContext context,
        SubjectId subject,
        string? language,
        string? timeZone,
        IReadOnlyDictionary<string, string> declared,
        string reason,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(declared);
        ArgumentNullException.ThrowIfNull(reason);

        // IDN-READ-004: the setting stays a person's, so no system principal makes it.
        if (context.Acting is not SubjectId acting)
        {
            return Result.Failure(Error.From(ErrorCodes.Denied));
        }

        if (await scope.RefusedAsync(context, Permissions.AccountManage, cancellationToken).ConfigureAwait(false)
            is Error refused)
        {
            return Result.Failure(refused);
        }

        // API-CONV-002: the reason the change is recorded with is free text of 1 to 1024
        // characters after trimming.
        if (reason.Trim() is not { Length: > 0 and <= 1024 } stated)
        {
            return Result.Failure(
                Error.From(ErrorCodes.RequestMalformed, "member", JsonSerializer.SerializeToElement("reason")));
        }

        // IDN-READ-003: a deleted account is answered as an account no person bears.
        if (await directory.StateAsync(subject, cancellationToken).ConfigureAwait(false)
            is null or AccountState.Deleted)
        {
            return Result.Failure(Error.From(ErrorCodes.AccountNotFound));
        }

        Result<int> maximumSize = await configuration
            .ReadAsync(Settings.PreferencesMaxSize, cancellationToken)
            .ConfigureAwait(false);

        if (maximumSize.Match(_ => (Error?)null, error => error) is Error unread)
        {
            return Result.Failure(unread);
        }

        DateTimeOffset now = time.GetUtcNow();

        if ((await work.BeginAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(_ => null, error => error) is Error notBegun)
        {
            return Result.Failure(notBegun);
        }

        // AUTHZ-GATE-006, D-183: the gate is asked again inside the unit of work, with the
        // acting account's row held before any other lock, so a restriction committed since
        // the gate step refuses the change before anything is written.
        if (await AskedAgainAsync(context, acting, subject, cancellationToken).ConfigureAwait(false) is Error since)
        {
            await work.RollbackAsync().ConfigureAwait(false);

            return Result.Failure(since);
        }

        // REG-PREF-001: an administrator's replace sets a key only an administrator may
        // edit, validated as the person's own setting is.
        if ((await directory
                .RecordPreferencesAsync(
                    subject,
                    language,
                    timeZone,
                    declared,
                    asAdministrator: true,
                    maximumSize.Match(read => read, _ => default),
                    cancellationToken)
                .ConfigureAwait(false))
            .Match<Error?>(() => null, error => error) is Error invalid)
        {
            await work.RollbackAsync().ConfigureAwait(false);

            return Result.Failure(invalid);
        }

        await audit
            .PreferencesSetAsync(acting, context.BreakGlassReason, subject, stated, now, cancellationToken)
            .ConfigureAwait(false);

        if ((await work.CommitAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(() => null, error => error) is Error notCommitted)
        {
            return Result.Failure(notCommitted);
        }

        return Result.Success();
    }

    // What a suspension is refused with, where it is; an account an administrator
    // already suspended is settled with nothing to do, which is a refusal of nothing
    // and no state; otherwise the state it is suspended from.
    // AUTHZ-GATE-006, D-183: the gate asked again inside the unit of work. Where the
    // account acted on is the acting one, the operation goes on to lock that row itself,
    // so it is taken for the change first and the gate judges it under that lock.
    private async ValueTask<Error?> AskedAgainAsync(
        AccessContext context,
        SubjectId acting,
        SubjectId subject,
        CancellationToken cancellationToken)
    {
        if (acting == subject)
        {
            await directory.HoldAsync(subject, cancellationToken).ConfigureAwait(false);
        }

        return await scope.RefusedAsync(context, Permissions.AccountManage, cancellationToken)
            .ConfigureAwait(false);
    }

    private async ValueTask<(Error? Refusal, AccountState? State)> SuspendableAsync(
        SubjectId subject,
        CancellationToken cancellationToken)
    {
        AccountState? state = await directory.StateAsync(subject, cancellationToken).ConfigureAwait(false);

        // OPS-BOOT-002: the break-glass session's account is never suspended, since it is
        // the way in an emergency leaves.
        if (state is not null
            && await emergency.FindAsync(cancellationToken).ConfigureAwait(false) == subject)
        {
            return (Error.From(ErrorCodes.Denied), null);
        }

        return state switch
        {
            null => (Error.From(ErrorCodes.AccountNotFound), null),
            AccountState.Deleting or AccountState.Deleted => (StateConflict(state.Value, suspendedBy: null), null),
            AccountState.Suspended
                when await directory.SuspendedByAsync(subject, cancellationToken).ConfigureAwait(false)
                    is SuspensionOrigin.Administrator => (null, null),
            _ => (null, state),
        };
    }

    private async ValueTask<Error?> ReactivationRefusedAsync(
        SubjectId subject,
        CancellationToken cancellationToken)
    {
        if (await directory.StateAsync(subject, cancellationToken).ConfigureAwait(false)
            is not AccountState state)
        {
            return Error.From(ErrorCodes.AccountNotFound);
        }

        // IDN-LIFE-013: the two suspensions are reversed differently, and what its owner
        // deactivated is theirs to stand back up.
        SuspensionOrigin? suspendedBy = state is AccountState.Suspended
            ? await directory.SuspendedByAsync(subject, cancellationToken).ConfigureAwait(false)
            : null;

        return suspendedBy is SuspensionOrigin.Administrator ? null : StateConflict(state, suspendedBy);
    }

    private async ValueTask<Error?> LiftRefusedAsync(
        SubjectId subject,
        CancellationToken cancellationToken) =>
        await directory.StateAsync(subject, cancellationToken).ConfigureAwait(false) switch
        {
            null => Error.From(ErrorCodes.AccountNotFound),

            // PRIV-RIGHT-004: a restriction held while the account is suspended or
            // deleting stays until the account is back in the restricted state.
            AccountState.Suspended => StateConflict(
                AccountState.Suspended,
                await directory.SuspendedByAsync(subject, cancellationToken).ConfigureAwait(false)),
            not AccountState.Restricted and AccountState other => StateConflict(other, suspendedBy: null),
            _ => null,
        };

    // What a cancellation is refused with, where it is, or the deletion it cancels.
    private async ValueTask<(Error? Refusal, HeldDeletion? Deleting)> CancellableAsync(
        SubjectId subject,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (await directory.StateAsync(subject, cancellationToken).ConfigureAwait(false)
            is not AccountState state)
        {
            return (Error.From(ErrorCodes.AccountNotFound), null);
        }

        if (await directory.DeletingAsync(subject, cancellationToken).ConfigureAwait(false)
            is not HeldDeletion deleting)
        {
            return (StateConflict(
                state,
                state is AccountState.Suspended
                    ? await directory.SuspendedByAsync(subject, cancellationToken).ConfigureAwait(false)
                    : null), null);
        }

        // IDN-LIFE-003: a takedown is reversed through its own operation, never cancelled.
        if (deleting.By is DeletionOrigin.Takedown)
        {
            return (Error.From(ErrorCodes.TakedownActive), null);
        }

        TimeSpan grace = (await configuration
                .ReadAsync(Settings.AccountDeletionGrace, cancellationToken).ConfigureAwait(false))
            .Match(read => read, error => throw new InvalidOperationException(error.Code.ToString()));

        return now >= deleting.Since + grace
            ? (Error.From(ErrorCodes.DeletionWindowElapsed), null)
            : (null, deleting);
    }

    // IDN-READ-004: the permission asked where the read is asked, then the account found
    // within the reach of that organization.
    private async ValueTask<Result<AccountState>> ReachedAsync(
        AccessContext context,
        Permission permission,
        SubjectId subject,
        OrganizationId? organization,
        CancellationToken cancellationToken) =>
        await (await reach.AskedAsync(context, permission, organization, cancellationToken).ConfigureAwait(false))
            .Match(
                read => WithinAsync(read, subject, cancellationToken),
                error => ValueTask.FromResult(Result.Failure<AccountState>(error)))
            .ConfigureAwait(false);

    // REC-ADM-001: a record's name, its contacts as held, verified or not, its language
    // and its state, with no time zone; a deleted record, and one beyond the reach, are
    // answered as an account no person bears, in the same bytes.
    private async ValueTask<Result<ContactDetail>> RecordContactAsync(
        AccessContext context,
        SubjectId subject,
        RecordStanding standing,
        OrganizationId? organization,
        CancellationToken cancellationToken)
    {
        Result<AdmittedRead> asked = await reach
            .AskedAsync(context, Permissions.ContactRead, organization, cancellationToken)
            .ConfigureAwait(false);

        if (asked.Match(_ => (Error?)null, error => error) is Error refused)
        {
            return Result.Failure<ContactDetail>(refused);
        }

        if (standing.State is PersonRecordState.Deleted
            || !await asked
                .Match(
                    read => reach.ReachesAsync(read, subject, cancellationToken),
                    _ => ValueTask.FromResult(false))
                .ConfigureAwait(false)
            || await records.ReadAsync(subject, cancellationToken).ConfigureAwait(false) is not ReadRecord record)
        {
            return Result.Failure<ContactDetail>(Error.From(ErrorCodes.AccountNotFound));
        }

        var contact = new ContactDetail(
            SubjectKind.Record,
            record.Name,
            record.Contacts.FirstOrDefault(held => held.Kind is IdentifierKind.Email)?.Value,
            record.Contacts.FirstOrDefault(held => held.Kind is IdentifierKind.Phone)?.Value,
            record.Language,
            TimeZone: null,
            record.State switch
            {
                PersonRecordState.Restricted => AccountState.Restricted,
                PersonRecordState.Deleting => AccountState.Deleting,
                _ => AccountState.Active,
            });

        return await CountedAsync(context, contact, cancellationToken).ConfigureAwait(false);
    }

    // IDN-READ-003, IDN-READ-004: a deleted account, and one beyond the reach, are
    // answered as an account no person bears, in the same bytes.
    private async ValueTask<Result<AccountState>> WithinAsync(
        AdmittedRead read,
        SubjectId subject,
        CancellationToken cancellationToken)
    {
        AccountState? state = await directory.StateAsync(subject, cancellationToken).ConfigureAwait(false);

        return state is null or AccountState.Deleted
            || !await reach.ReachesAsync(read, subject, cancellationToken).ConfigureAwait(false)
            ? Result.Failure<AccountState>(Error.From(ErrorCodes.AccountNotFound))
            : Result.Success(state.Value);
    }

    // OPS-ALERT-005: each person record a person reads counts toward their read volume;
    // a system principal's reads are nobody's and are not counted.
    private async ValueTask<Result<TRecord>> CountedAsync<TRecord>(
        AccessContext context,
        TRecord record,
        CancellationToken cancellationToken) =>
        (await volume.ReturnedAsync(context, 1, cancellationToken).ConfigureAwait(false))
            .Match(() => Result.Success(record), Result.Failure<TRecord>);

    private static List<AccountViewIdentifier> Shown(HeldIdentifiers held, IdentifierKind kind)
    {
        IReadOnlyList<HeldIdentifier> ofKind = held.OfKind(kind);

        var shown = new List<AccountViewIdentifier>(ofKind.Count);

        foreach (HeldIdentifier identifier in ofKind)
        {
            shown.Add(new AccountViewIdentifier(identifier.Entered, identifier.IsVerified, identifier.IsPrimary));
        }

        return shown;
    }

    // IDN-READ-003: the primary of the kind where it is verified, which is the one a
    // host writes to; an account holding none verified has nothing to reach.
    private static string? Reachable(HeldIdentifiers held, IdentifierKind kind)
    {
        foreach (HeldIdentifier identifier in held.OfKind(kind))
        {
            if (identifier.IsPrimary && identifier.IsVerified)
            {
                return identifier.Entered;
            }
        }

        return null;
    }

    private static string Key(SubjectId subject, DateTimeOffset at) =>
        string.Create(CultureInfo.InvariantCulture, $"{subject.Value}@{at.UtcTicks}");

    // Chapter 10 section 1.1: the state is named as section 5.1 spells it and, where it
    // is suspended, who suspended it as section 5.12b does.
    internal static Error StateConflict(AccountState state, SuspensionOrigin? suspendedBy)
    {
        var details = new Dictionary<string, JsonElement>(capacity: 2, StringComparer.Ordinal)
        {
            ["state"] = JsonSerializer.SerializeToElement(WrittenName.Of(state)),
        };

        if (suspendedBy is SuspensionOrigin by)
        {
            details["suspendedBy"] = JsonSerializer.SerializeToElement(WrittenName.Of(by));
        }

        return new Error(ErrorCodes.AccountStateConflict, details);
    }
}
