using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Privacy.Documents;

namespace Janus.Privacy.Consents;

/// <summary>
/// The consent and objection records one subject holds.
/// </summary>
/// <param name="consents">Where the records are.</param>
/// <param name="documents">Where the version a record is given against is read.</param>
/// <param name="processing">What the deployment declared it processes and on what basis.</param>
/// <param name="events">Where the change is announced.</param>
/// <param name="audit">Where the change is written down.</param>
/// <param name="work">The one transaction an operation runs in.</param>
/// <param name="time">The clock the deployment runs on.</param>
/// <remarks>
/// Implements LIB-API-005, PRIV-CONS-001, PRIV-CONS-002, PRIV-CONS-008,
/// PRIV-CONS-011 and PRIV-RIGHT-001a. One purpose a record: nothing here takes a list,
/// so no record can reference two purposes and no one control can grant for two.
/// </remarks>
internal sealed class ConsentService(
    IConsentStore consents,
    ILegalDocumentStore documents,
    DeclaredProcessing processing,
    IEvents events,
    IPrivacyAudit audit,
    IUnitOfWork work,
    TimeProvider time) : IConsents
{
    /// <summary>
    /// The document that governs a consent whose purpose names none, and whose
    /// version that consent is recorded against (PRIV-CONS-001, PRIV-CONS-007).
    /// </summary>
    internal const string Notice = "privacy-notice";

    private static readonly AuditAction Granted = AuditActions.ConsentGranted;

    private static readonly AuditAction Withdrawn = AuditActions.ConsentWithdrawn;

    private static readonly AuditAction Objected = AuditActions.ObjectionRecorded;

    private static readonly AuditAction Resumed = AuditActions.ObjectionWithdrawn;

    /// <inheritdoc/>
    public async ValueTask<Result<IReadOnlyList<ConsentRecord>>> ReadAsync(
        AccessContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        return context.Effective is not SubjectId subject
            ? Result.Failure<IReadOnlyList<ConsentRecord>>(Error.From(ErrorCodes.Denied))
            : Result.Success(await consents
                .ConsentsAsync(subject, cancellationToken)
                .ConfigureAwait(false));
    }

    /// <inheritdoc/>
    public async ValueTask<Result> GrantAsync(
        AccessContext context,
        string purpose,
        ConsentMechanism mechanism,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrWhiteSpace(purpose);

        if (context.Effective is not SubjectId subject)
        {
            return Result.Failure(Error.From(ErrorCodes.Denied));
        }

        // A purpose that rests on another basis is not the subject's to agree to, and
        // recording their agreement would imply it were (PRIV-CONS-008a).
        if (processing.Find(purpose) is not { Consent: ConsentKind kind } declared)
        {
            return Result.Failure(Error.From(ErrorCodes.PurposeNoConsent));
        }

        // A consent is given against the version in force of the document that
        // governs it, so before any version of that document is published there is
        // nothing for it to stand against (PRIV-CONS-005).
        if (await VersionAsync(declared.Document, cancellationToken).ConfigureAwait(false)
            is not string version)
        {
            return Result.Failure(Error.From(ErrorCodes.NoticeUnpublished));
        }

        string document = declared.Document ?? Notice;

        // PRIV-CONS-001 AC6: a live record the purpose admits is the consent this grant
        // asks for, so the answer is the grant's and nothing is written, announced or
        // recorded.
        if (Standing(await consents.ConsentsAsync(subject, cancellationToken).ConfigureAwait(false), purpose)
            is { Live: true } admitted
            && Admits(admitted, document, kind))
        {
            return Result.Success();
        }

        DateTimeOffset now = time.GetUtcNow();

        Result<bool> begun = await work.BeginAsync(cancellationToken).ConfigureAwait(false);

        if (begun.Match<Error?>(_ => null, error => error) is Error notBegun)
        {
            return Result.Failure(notBegun);
        }

        // D-188: the registration calls this joined, the endpoint as the outermost.
        bool outermost = begun.Match(level => level, _ => false);

        await consents.HoldAsync(subject, cancellationToken).ConfigureAwait(false);

        bool ended = false;
        bool added = false;

        // PRIV-CONS-001 AC6: the grant is decided on the record as its transaction reads
        // it, and added only where no live record stands; where one was written
        // meanwhile the addition is not made, and the grant is decided again on that
        // record.
        while (!added)
        {
            ConsentRecord? standing = Standing(
                await consents.ConsentsAsync(subject, cancellationToken).ConfigureAwait(false),
                purpose);

            if (standing is { Live: true })
            {
                if (Admits(standing, document, kind))
                {
                    break;
                }

                // A live record the purpose no longer admits is ended by the grant that
                // replaces it, in that grant's transaction.
                ended |= await consents
                    .SupersedeAsync(subject, purpose, now, cancellationToken)
                    .ConfigureAwait(false);
            }

            // PRIV-CONS-001, PRIV-CONS-007: a grant from the subject's own pages over a
            // consent that was ended, and the subject never took back, is the answer to
            // being asked again, whoever calls the contract.
            if (mechanism is ConsentMechanism.Dashboard
                && (ended || standing is { SupersededAt: not null, WithdrawnAt: null }))
            {
                mechanism = ConsentMechanism.Reconsent;
            }

            added = await consents
                .AddAsync(
                    subject,
                    new ConsentRecord(
                        purpose,
                        document,
                        version,
                        mechanism,
                        kind,
                        now,
                        WithdrawnAt: null,
                        SupersededAt: null),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        if (ended
            && await AnnouncedAsync(subject, purpose, ConsentChange.Superseded, now, cancellationToken)
                .ConfigureAwait(false) is Error unended)
        {
            await work.RollbackAsync().ConfigureAwait(false);

            return Result.Failure(unended);
        }

        if (!added)
        {
            // CONV-DESIGN-003: a record written meanwhile is the consent this asks for. A
            // success that wrote nothing rolls back only where this level is the
            // outermost; one that joined another operation's commits, so the whole is not
            // marked, and so does one that ended a record.
            if (outermost && !ended)
            {
                await work.RollbackAsync().ConfigureAwait(false);

                return Result.Success();
            }

            return await work.CommitAsync(cancellationToken).ConfigureAwait(false);
        }

        if (await AnnouncedAsync(subject, purpose, ConsentChange.Granted, now, cancellationToken)
                .ConfigureAwait(false) is Error unannounced)
        {
            await work.RollbackAsync().ConfigureAwait(false);

            return Result.Failure(unannounced);
        }

        await audit
            .RecordedAsync(
                Granted,
                context.Acting,
                context.BreakGlassReason,
                subject,
                now,
                Named(purpose, version, mechanism, kind),
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
    public async ValueTask<Result> WithdrawAsync(
        AccessContext context,
        string purpose,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrWhiteSpace(purpose);

        if (context.Effective is not SubjectId subject)
        {
            return Result.Failure(Error.From(ErrorCodes.Denied));
        }

        // Withdrawal reaches the same purposes a grant does: one that is undeclared or
        // rests on another basis holds no consent to withdraw (PRIV-CONS-008a).
        if (processing.Find(purpose) is not { Consent: not null })
        {
            return Result.Failure(Error.From(ErrorCodes.PurposeNoConsent));
        }

        IReadOnlyList<ConsentRecord> held = await consents
            .ConsentsAsync(subject, cancellationToken)
            .ConfigureAwait(false);

        // PRIV-CONS-008 AC5: a consent the subject does not hold is withdrawn already,
        // so the answer is the withdrawal's and nothing is written, announced or
        // recorded.
        if (Standing(held, purpose) is not { WithdrawnAt: null })
        {
            return Result.Success();
        }

        DateTimeOffset now = time.GetUtcNow();

        if ((await work.BeginAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(_ => null, error => error) is Error notBegun)
        {
            return Result.Failure(notBegun);
        }

        // D-166 X3: the consent is read again with the subject's records held, and the
        // record that stands is stamped only where it has not been taken back, so a
        // withdrawal at the same moment is announced and recorded once.
        await consents.HoldAsync(subject, cancellationToken).ConfigureAwait(false);

        if (Standing(await consents.ConsentsAsync(subject, cancellationToken).ConfigureAwait(false), purpose)
                is not { WithdrawnAt: null } consent
            || !await consents.WithdrawConsentAsync(subject, purpose, now, cancellationToken).ConfigureAwait(false))
        {
            // CONV-DESIGN-003: taken back meanwhile, so nothing was written.
            await work.RollbackAsync().ConfigureAwait(false);

            return Result.Success();
        }

        if (await AnnouncedAsync(subject, purpose, ConsentChange.Withdrawn, now, cancellationToken)
                .ConfigureAwait(false) is Error unannounced)
        {
            await work.RollbackAsync().ConfigureAwait(false);

            return Result.Failure(unannounced);
        }

        await audit
            .RecordedAsync(
                Withdrawn,
                context.Acting,
                context.BreakGlassReason,
                subject,
                now,
                Named(purpose, consent.NoticeVersion, consent.Mechanism, consent.Kind),
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
    public async ValueTask<Result<IReadOnlyList<ObjectionRecord>>> ObjectionsAsync(
        AccessContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        return context.Effective is not SubjectId subject
            ? Result.Failure<IReadOnlyList<ObjectionRecord>>(Error.From(ErrorCodes.Denied))
            : Result.Success(await consents
                .ObjectionsAsync(subject, cancellationToken)
                .ConfigureAwait(false));
    }

    /// <inheritdoc/>
    public async ValueTask<Result> ObjectAsync(
        AccessContext context,
        string purpose,
        ConsentMechanism mechanism,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrWhiteSpace(purpose);

        if (context.Effective is not SubjectId subject)
        {
            return Result.Failure(Error.From(ErrorCodes.Denied));
        }

        if (processing.Find(purpose) is not { Basis.IsObjectable: true })
        {
            return Result.Failure(Error.From(
                ErrorCodes.PurposeNotObjectable,
                "purpose",
                JsonSerializer.SerializeToElement(purpose)));
        }

        // PRIV-RIGHT-001a AC6: an objection is recorded against the privacy notice, so
        // before any version of it is published there is nothing for it to stand against.
        if (await VersionAsync(document: null, cancellationToken).ConfigureAwait(false)
            is not string version)
        {
            return Result.Failure(Error.From(ErrorCodes.NoticeUnpublished));
        }

        // PRIV-RIGHT-001a AC6: an objection that stands is the one this asks for, so the
        // answer is the objection's and nothing is written, announced or recorded.
        if (Standing(await consents.ObjectionsAsync(subject, cancellationToken).ConfigureAwait(false), purpose)
            is not null)
        {
            return Result.Success();
        }

        DateTimeOffset now = time.GetUtcNow();
        var objection = new ObjectionRecord(purpose, Notice, version, mechanism, now, WithdrawnAt: null);

        if ((await work.BeginAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(_ => null, error => error) is Error notBegun)
        {
            return Result.Failure(notBegun);
        }

        // It is added only where none stands, so one recorded meanwhile leaves this one
        // unwritten and is the objection this asks for.
        if (!await consents.AddAsync(subject, objection, cancellationToken).ConfigureAwait(false))
        {
            // CONV-DESIGN-003: nothing was written.
            await work.RollbackAsync().ConfigureAwait(false);

            return Result.Success();
        }

        if (await ObjectedAsync(subject, purpose, objecting: true, now, cancellationToken)
                .ConfigureAwait(false) is Error unannounced)
        {
            await work.RollbackAsync().ConfigureAwait(false);

            return Result.Failure(unannounced);
        }

        await audit
            .RecordedAsync(
                Objected,
                context.Acting,
                context.BreakGlassReason,
                subject,
                now,
                Named(purpose, version, mechanism),
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
    public async ValueTask<Result> WithdrawObjectionAsync(
        AccessContext context,
        string purpose,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrWhiteSpace(purpose);

        if (context.Effective is not SubjectId subject)
        {
            return Result.Failure(Error.From(ErrorCodes.Denied));
        }

        if (processing.Find(purpose) is not { Basis.IsObjectable: true })
        {
            return Result.Failure(Error.From(
                ErrorCodes.PurposeNotObjectable,
                "purpose",
                JsonSerializer.SerializeToElement(purpose)));
        }

        IReadOnlyList<ObjectionRecord> held = await consents
            .ObjectionsAsync(subject, cancellationToken)
            .ConfigureAwait(false);

        // PRIV-RIGHT-001a AC6: an objection the subject has not made is withdrawn
        // already, so the answer is the withdrawal's and nothing is written.
        if (Standing(held, purpose) is null)
        {
            return Result.Success();
        }

        DateTimeOffset now = time.GetUtcNow();

        if ((await work.BeginAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(_ => null, error => error) is Error notBegun)
        {
            return Result.Failure(notBegun);
        }

        // D-166 X3: read again with the subject's records held, as for a consent.
        await consents.HoldAsync(subject, cancellationToken).ConfigureAwait(false);

        if (Standing(await consents.ObjectionsAsync(subject, cancellationToken).ConfigureAwait(false), purpose)
                is not ObjectionRecord objection
            || !await consents.WithdrawObjectionAsync(subject, purpose, now, cancellationToken).ConfigureAwait(false))
        {
            // CONV-DESIGN-003: taken back meanwhile, so nothing was written.
            await work.RollbackAsync().ConfigureAwait(false);

            return Result.Success();
        }

        if (await ObjectedAsync(subject, purpose, objecting: false, now, cancellationToken)
                .ConfigureAwait(false) is Error unannounced)
        {
            await work.RollbackAsync().ConfigureAwait(false);

            return Result.Failure(unannounced);
        }

        await audit
            .RecordedAsync(
                Resumed,
                context.Acting,
                context.BreakGlassReason,
                subject,
                now,
                Named(purpose, objection.NoticeVersion, objection.Mechanism),
                cancellationToken)
            .ConfigureAwait(false);

        if ((await work.CommitAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(() => null, error => error) is Error notCommitted)
        {
            return Result.Failure(notCommitted);
        }

        return Result.Success();
    }

    // The record that stands for a purpose among the records a subject holds, oldest
    // first: the live one, or the latest where none is live.
    private static ConsentRecord? Standing(IReadOnlyList<ConsentRecord> held, string purpose) =>
        held.LastOrDefault(record => record.Live && string.Equals(record.Purpose, purpose, StringComparison.Ordinal))
        ?? held.LastOrDefault(record => string.Equals(record.Purpose, purpose, StringComparison.Ordinal));

    private static ObjectionRecord? Standing(IReadOnlyList<ObjectionRecord> held, string purpose) =>
        held.LastOrDefault(record =>
            record.Standing && string.Equals(record.Purpose, purpose, StringComparison.Ordinal));

    // PRIV-CONS-001, AUTHZ-GATE-002: a purpose admits a record given against the document
    // it now names, written where it requires the written path, as the gate reads one.
    private static bool Admits(ConsentRecord record, string document, ConsentKind required) =>
        string.Equals(record.Document, document, StringComparison.Ordinal)
        && (required is ConsentKind.Ordinary || record.Kind is ConsentKind.Written);

    private static string Key(SubjectId subject, string purpose, string change, DateTimeOffset at) =>
        subject.ToString()
        + ":" + purpose
        + ":" + change
        + "@" + at.ToString("O", CultureInfo.InvariantCulture);

    private static Dictionary<string, JsonElement> Named(
        string purpose,
        string version,
        ConsentMechanism mechanism) =>
        new(capacity: 3, StringComparer.Ordinal)
        {
            ["purpose"] = JsonSerializer.SerializeToElement(purpose),
            ["noticeVersion"] = JsonSerializer.SerializeToElement(version),
            ["mechanism"] = JsonSerializer.SerializeToElement(mechanism),
        };

    private static Dictionary<string, JsonElement> Named(
        string purpose,
        string version,
        ConsentMechanism mechanism,
        ConsentKind kind)
    {
        Dictionary<string, JsonElement> details = Named(purpose, version, mechanism);

        details["kind"] = JsonSerializer.SerializeToElement(kind);

        return details;
    }

    // PRIV-CONS-001, PRIV-CONS-007: a consent is recorded against the version of the
    // document its purpose names, and the privacy notice where it names none, which
    // is the same document a material revision of ends it.
    private async ValueTask<string?> VersionAsync(
        string? document,
        CancellationToken cancellationToken) =>
        (await documents.CurrentAsync(document ?? Notice, cancellationToken).ConfigureAwait(false))
        ?.Version;

    private async ValueTask<Error?> AnnouncedAsync(
        SubjectId subject,
        string purpose,
        ConsentChange change,
        DateTimeOffset at,
        CancellationToken cancellationToken) =>
        (await events
                .PublishAsync(
                    new ConsentChanged(at, Key(subject, purpose, change.ToString(), at), purpose, change)
                    {
                        Subject = subject,
                    },
                    cancellationToken)
                .ConfigureAwait(false))
            .Match(() => (Error?)null, error => error);

    private async ValueTask<Error?> ObjectedAsync(
        SubjectId subject,
        string purpose,
        bool objecting,
        DateTimeOffset at,
        CancellationToken cancellationToken) =>
        (await events
                .PublishAsync(
                    new ObjectionChanged(
                        at,
                        Key(subject, purpose, objecting ? "objecting" : "resumed", at),
                        purpose,
                        objecting)
                    {
                        Subject = subject,
                    },
                    cancellationToken)
                .ConfigureAwait(false))
            .Match(() => (Error?)null, error => error);
}
