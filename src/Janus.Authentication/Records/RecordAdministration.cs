using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Accounts;
using Janus.Authentication.Factors;
using Janus.Authentication.Organizations;
using Janus.Core;
using Janus.Core.Configuration;

namespace Janus.Authentication.Records;

/// <summary>
/// What staff do to a person record for the person who reaches it by no code: correct
/// it on their word, lift its restriction and cancel its deletion.
/// </summary>
/// <param name="records">The person records.</param>
/// <param name="admission">Where a path naming a record is admitted.</param>
/// <param name="recordAudit">Where a change of the record's fields is recorded.</param>
/// <param name="audit">Where a lift or a cancellation is recorded, as an account's is.</param>
/// <param name="accounts">Where the erasure request that began a window is read.</param>
/// <param name="stepUp">What the gated rows ask of the staff member's session.</param>
/// <param name="events">Where a cancellation is announced.</param>
/// <param name="configuration">Where the message languages and the deletion window are read.</param>
/// <param name="work">The one transaction each change runs in.</param>
/// <param name="time">The clock the deployment runs on.</param>
/// <remarks>
/// Implements REC-ADM-001, REC-ADM-002 and chapter 09 section 8a. Each row is admitted
/// in the organization that holds the record, judged first on where the record stands,
/// then, inside the unit of work, admitted again and judged again on the record's row
/// under its lock (AUTHZ-GATE-006, D-183), so a refusal writes nothing.
/// </remarks>
internal sealed class RecordAdministration(
    IRecordDirectory records,
    IReadAdmission admission,
    IRecordAudit recordAudit,
    IAccountAudit audit,
    IAccountDirectory accounts,
    StepUpGuard stepUp,
    IEvents events,
    IConfigurationStore configuration,
    IUnitOfWork work,
    TimeProvider time)
{
    private const int ReasonMaximum = 1024;

    /// <summary>
    /// Admits a row whose path names a record (D-222): asked in the organization that
    /// holds it and admitted where the permission is held there or in the administrative
    /// organization, as one admission; a caller admitted in neither is answered as a
    /// subject no record bears, the refusal recorded against the record's organization.
    /// </summary>
    /// <param name="context">Who is asking.</param>
    /// <param name="permission">The row's permission.</param>
    /// <param name="subject">The record's subject.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>Where the record stands, or the refusal.</returns>
    public async ValueTask<Result<RecordStanding>> AdmittedAsync(
        AccessContext context,
        Permission permission,
        SubjectId subject,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (await records.StandingAsync(subject, cancellationToken).ConfigureAwait(false) is not RecordStanding standing)
        {
            return Result.Failure<RecordStanding>(Error.From(ErrorCodes.RecordNotFound));
        }

        if (await admission.RefusedAsync(context, permission, standing.Organization, cancellationToken).ConfigureAwait(false)
            is not Error refused)
        {
            return Result.Success(standing);
        }

        return Result.Failure<RecordStanding>(
            refused.Code == ErrorCodes.Denied ? Error.From(ErrorCodes.RecordNotFound) : refused);
    }

    /// <summary>
    /// Corrects a record on the person's word: a contact changed is held unverified
    /// from then on, so a code never reaches a contact staff wrote.
    /// </summary>
    /// <param name="context">The staff member correcting it.</param>
    /// <param name="subject">The record's subject.</param>
    /// <param name="correction">What is corrected, and why.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>Success, or the refusal <see cref="IPersonRecords.RectifyAsync"/> names.</returns>
    public async ValueTask<Result> RectifyAsync(
        AccessContext context,
        SubjectId subject,
        PersonRecordCorrection correction,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(correction);

        return await (await AdmittedAsync(context, Permissions.RecordManage, subject, cancellationToken).ConfigureAwait(false))
            .Match(
                admitted => CorrectedAsync(context, subject, admitted, correction, cancellationToken),
                error => ValueTask.FromResult(Result.Failure(error)))
            .ConfigureAwait(false);
    }

    private async ValueTask<Result> CorrectedAsync(
        AccessContext context,
        SubjectId subject,
        RecordStanding admitted,
        PersonRecordCorrection correction,
        CancellationToken cancellationToken)
    {
        if (context.Acting is not SubjectId acting)
        {
            return Result.Failure(Error.From(ErrorCodes.Denied));
        }

        if (Written(correction.Reason) is not string reason)
        {
            return Result.Failure(Malformed("reason"));
        }

        IReadOnlyList<string> languages = (await configuration
                .ReadAsync(Settings.NotificationLanguages, cancellationToken).ConfigureAwait(false))
            .Match(read => read, error => throw new InvalidOperationException(error.Code.ToString()));

        if (Corrected(correction, languages, out Correction corrected) is Error invalid)
        {
            return Result.Failure(invalid);
        }

        return await ChangedAsync(
                context,
                Permissions.RecordManage,
                subject,
                admitted,
                Uncorrectable,
                challenge: null,
                async (_, token) =>
                {
                    await records.RectifyAsync(subject, corrected.Name, corrected.Language, token).ConfigureAwait(false);

                    List<string> fields = [];

                    if (corrected.Name is not null)
                    {
                        fields.Add("name");
                    }

                    if (corrected.Language is not null)
                    {
                        fields.Add("language");
                    }

                    IReadOnlyList<CollectedContact> held =
                        (await records.ReadAsync(subject, token).ConfigureAwait(false))?.Contacts ?? [];

                    // REC-ADM-002 AC2: a contact staff wrote is held unverified, and one
                    // given as it is held keeps what it held.
                    foreach ((IdentifierKind kind, string value) in corrected.Contacts)
                    {
                        if (held.Any(contact => contact.Kind == kind && string.Equals(contact.Value, value, StringComparison.Ordinal)))
                        {
                            continue;
                        }

                        await records.CorrectContactAsync(subject, kind, value, token).ConfigureAwait(false);
                        fields.Add(kind is IdentifierKind.Email ? "email" : "phone");
                    }

                    DateTimeOffset now = time.GetUtcNow();

                    // IDN-AUD-001: a change is recorded by field, never by value, with the
                    // staff member acting and the reason they gave.
                    foreach (string field in fields)
                    {
                        await recordAudit
                            .ChangedAsync(acting, context.BreakGlassReason, subject, field, reason, now, token)
                            .ConfigureAwait(false);
                    }

                    return null;
                },
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Lifts a record's restriction, the record returning active and the change
    /// announced to the subscribers.
    /// </summary>
    /// <param name="context">The staff member lifting it.</param>
    /// <param name="session">The session the step-up is judged on.</param>
    /// <param name="subject">The record's subject.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>Success, or the refusal <see cref="IPersonRecords.LiftRestrictionAsync"/> names.</returns>
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

        return await (await AdmittedAsync(context, Permissions.RecordManage, subject, cancellationToken).ConfigureAwait(false))
            .Match(
                admitted => ChangedAsync(
                    context,
                    Permissions.RecordManage,
                    subject,
                    admitted,
                    Unliftable,
                    (acting, session, StepUpAction.RecordRestrictionLift),
                    async (_, token) =>
                    {
                        DateTimeOffset now = time.GetUtcNow();

                        await records.LiftRestrictionAsync(subject, now, token).ConfigureAwait(false);
                        await audit
                            .AdministeredAsync(AuditActions.RestrictionLifted, acting, context.BreakGlassReason, subject, now, token)
                            .ConfigureAwait(false);

                        return null;
                    },
                    cancellationToken),
                error => ValueTask.FromResult(Result.Failure(error)))
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Cancels a record's deletion begun by the person or by a request that arrived out
    /// of band, restoring the record as it stood.
    /// </summary>
    /// <param name="context">The staff member cancelling it.</param>
    /// <param name="session">The session the step-up is judged on.</param>
    /// <param name="subject">The record's subject.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>Success, or the refusal <see cref="IPersonRecords.CancelDeletionAsync"/> names.</returns>
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

        TimeSpan grace = (await configuration
                .ReadAsync(Settings.RecordDeletionGrace, cancellationToken).ConfigureAwait(false))
            .Match(read => read, error => throw new InvalidOperationException(error.Code.ToString()));

        return await (await AdmittedAsync(context, Permissions.RecordManage, subject, cancellationToken).ConfigureAwait(false))
            .Match(
                admitted => ChangedAsync(
                    context,
                    Permissions.RecordManage,
                    subject,
                    admitted,
                    standing => Uncancellable(standing, time.GetUtcNow() - grace),
                    (acting, session, StepUpAction.RecordDeletionCancel),
                    async (standing, token) =>
                    {
                        DateTimeOffset now = time.GetUtcNow();

                        // IDN-LIFE-003: the cancellation of a window an out-of-band
                        // request began is recorded against that request.
                        PrivacyRequestId? request = standing is
                        {
                            DeletingBy: DeletionOrigin.OutOfBandRequest,
                            DeletingSince: DateTimeOffset since,
                        }
                            ? await accounts.ErasureRequestAsync(subject, since, token).ConfigureAwait(false)
                            : null;

                        await records.CancelDeletionAsync(subject, token).ConfigureAwait(false);

                        if ((await events
                                .PublishAsync(
                                    new RecordDeletionCancelled(now, Key(subject, now))
                                    {
                                        Subject = subject,
                                        Actor = acting,
                                        Effective = context.Effective,
                                    },
                                    token)
                                .ConfigureAwait(false))
                            .Match(() => (Error?)null, error => error) is Error unpublished)
                        {
                            return unpublished;
                        }

                        await audit
                            .CancelledOnBehalfAsync(acting, context.BreakGlassReason, subject, request, now, token)
                            .ConfigureAwait(false);

                        return null;
                    },
                    cancellationToken),
                error => ValueTask.FromResult(Result.Failure(error)))
            .ConfigureAwait(false);
    }

    // REC-ADM-002: a record attached to an account is the account's, and one in its
    // deletion window, or erased, takes no correction; a restricted one does, being how
    // a rectification request is fulfilled (AUTHZ-GATE-006, D-222).
    private static Error? Uncorrectable(RecordStanding standing) =>
        Followed(standing)
        ?? (standing.State is PersonRecordState.Deleting or PersonRecordState.Deleted
            ? StateConflict(standing.State)
            : null);

    // PRIV-RIGHT-004, 09 section 8a: only a restricted record is lifted; one holding a
    // restriction in its deletion window is not.
    private static Error? Unliftable(RecordStanding standing) =>
        Followed(standing)
        ?? (standing.State is PersonRecordState.Restricted ? null : StateConflict(standing.State));

    // REC-ADM-002: a deletion the person or an out-of-band request began is cancelled
    // inside its window; a takedown's is reversed, never cancelled.
    private static Error? Uncancellable(RecordStanding standing, DateTimeOffset closedBefore) =>
        Followed(standing)
        ?? standing switch
        {
            { State: PersonRecordState.Deleting, DeletingBy: DeletionOrigin.Takedown } =>
                Error.From(ErrorCodes.TakedownActive),
            {
                State: PersonRecordState.Deleting,
                DeletingBy: DeletionOrigin.Self or DeletionOrigin.OutOfBandRequest,
                DeletingSince: DateTimeOffset since,
            } => since <= closedBefore ? Error.From(ErrorCodes.DeletionWindowElapsed) : null,
            _ => StateConflict(standing.State),
        };

    private static string Key(SubjectId record, DateTimeOffset at) =>
        string.Create(CultureInfo.InvariantCulture, $"{record.Value}@{at.UtcTicks}");

    // REC-ATT-002: a record attached to an account follows it.
    private static Error? Followed(RecordStanding standing) =>
        standing.AttachedTo is SubjectId account
            ? Error.From(ErrorCodes.RecordFollowsAccount, "account", JsonSerializer.SerializeToElement(account.Value))
            : null;

    private static Error StateConflict(PersonRecordState state) =>
        Error.From(ErrorCodes.RecordStateConflict, "state", JsonSerializer.SerializeToElement(WrittenName.Of(state)));

    // D-221: a free-text reason is 1 to 1024 characters after trimming.
    private static string? Written(string? reason) =>
        reason?.Trim() is { Length: > 0 and <= ReasonMaximum } written ? written : null;

    private static Error Malformed(string member) =>
        Error.From(ErrorCodes.RequestMalformed, "member", JsonSerializer.SerializeToElement(member));

    // 09 section 8a: each member is judged as POST /admin/records judges it, in its order.
    private static Error? Corrected(
        PersonRecordCorrection correction,
        IReadOnlyList<string> languages,
        out Correction corrected)
    {
        corrected = new Correction(null, null, []);

        if (correction.Language is string language && !languages.Contains(language, StringComparer.Ordinal))
        {
            return Error.From(ErrorCodes.RequestInvalid, "member", JsonSerializer.SerializeToElement("language"));
        }

        DisplayName? name = null;

        if (correction.Name is string entered)
        {
            if (!DisplayName.TryParse(entered, out DisplayName parsed))
            {
                return Error.From(ErrorCodes.ProfileInvalid);
            }

            name = parsed;
        }

        List<(IdentifierKind, string)> contacts = [];

        if (correction.Email is string email)
        {
            if (!EmailAddress.TryParse(email.Trim(), out EmailAddress address))
            {
                return Error.From(ErrorCodes.IdentifierInvalid);
            }

            contacts.Add((IdentifierKind.Email, address.Value));
        }

        if (correction.Phone is string phone)
        {
            if (!PhoneNumber.TryParse(phone.Trim(), out PhoneNumber number))
            {
                return Error.From(ErrorCodes.IdentifierInvalid);
            }

            contacts.Add((IdentifierKind.Phone, number.Value));
        }

        corrected = new Correction(name, correction.Language, contacts);

        return null;
    }

    // The change judged first on where the admitted record stands, then decided on its
    // row under its lock in one unit of work, the admission asked again there, so a
    // record attached, erased or moved since is answered as it now stands.
    private async ValueTask<Result> ChangedAsync(
        AccessContext context,
        Permission permission,
        SubjectId subject,
        RecordStanding admitted,
        Func<RecordStanding, Error?> refusal,
        (SubjectId Acting, SessionId Session, StepUpAction Action)? challenge,
        Func<RecordStanding, CancellationToken, ValueTask<Error?>> change,
        CancellationToken cancellationToken)
    {
        if (refusal(admitted) is Error first)
        {
            return Result.Failure(first);
        }

        // 09 section 8a: the session's proof is judged after every other refusal, so a
        // request refused on what it asks never asks the staff member to step up.
        if (challenge is (SubjectId acting, SessionId session, StepUpAction action)
            && await stepUp.PassedAsync(acting, session, action, cancellationToken).ConfigureAwait(false)
                is Error challenged)
        {
            return Result.Failure(challenged);
        }

        if ((await work.BeginAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(_ => null, error => error) is Error notBegun)
        {
            return Result.Failure(notBegun);
        }

        Error? refused =
            (await AdmittedAsync(context, permission, subject, cancellationToken).ConfigureAwait(false))
                .Match(_ => (Error?)null, error => error)
            ?? (await records.HoldAsync(subject, cancellationToken).ConfigureAwait(false) is RecordStanding standing
                ? refusal(standing) ?? await change(standing, cancellationToken).ConfigureAwait(false)
                : Error.From(ErrorCodes.RecordNotFound));

        if (refused is not null)
        {
            await work.RollbackAsync().ConfigureAwait(false);

            return Result.Failure(refused);
        }

        return await work.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    private sealed record Correction(
        DisplayName? Name,
        string? Language,
        IReadOnlyList<(IdentifierKind Kind, string Value)> Contacts);
}
