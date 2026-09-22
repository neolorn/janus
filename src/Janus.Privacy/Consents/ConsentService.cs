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

        DateTimeOffset now = time.GetUtcNow();
        var granted = new ConsentRecord(
            purpose,
            version,
            mechanism,
            kind,
            now,
            WithdrawnAt: null,
            SupersededAt: null);

        await work.BeginAsync(cancellationToken).ConfigureAwait(false);
        await consents.RecordAsync(subject, granted, cancellationToken).ConfigureAwait(false);
        await AnnouncedAsync(subject, purpose, ConsentChange.Granted, now, cancellationToken)
            .ConfigureAwait(false);
        await audit
            .RecordedAsync(
                Granted,
                context.Acting,
                subject,
                now,
                Named(purpose, version, mechanism, kind),
                cancellationToken)
            .ConfigureAwait(false);
        await work.CommitAsync(cancellationToken).ConfigureAwait(false);

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

        if (Of(held, purpose) is not ConsentRecord consent)
        {
            return Result.Failure(Error.From(ErrorCodes.Denied));
        }

        if (consent.WithdrawnAt is not null)
        {
            return Result.Success();
        }

        DateTimeOffset now = time.GetUtcNow();

        await work.BeginAsync(cancellationToken).ConfigureAwait(false);
        await consents
            .RecordAsync(subject, consent with { WithdrawnAt = now }, cancellationToken)
            .ConfigureAwait(false);
        await AnnouncedAsync(subject, purpose, ConsentChange.Withdrawn, now, cancellationToken)
            .ConfigureAwait(false);
        await audit
            .RecordedAsync(
                Withdrawn,
                context.Acting,
                subject,
                now,
                Named(purpose, consent.NoticeVersion, consent.Mechanism, consent.Kind),
                cancellationToken)
            .ConfigureAwait(false);
        await work.CommitAsync(cancellationToken).ConfigureAwait(false);

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

        if (await VersionAsync(document: null, cancellationToken).ConfigureAwait(false)
            is not string version)
        {
            return Result.Failure(Error.From(ErrorCodes.Denied));
        }

        DateTimeOffset now = time.GetUtcNow();
        var objection = new ObjectionRecord(purpose, version, mechanism, now, WithdrawnAt: null);

        await work.BeginAsync(cancellationToken).ConfigureAwait(false);
        await consents.RecordAsync(subject, objection, cancellationToken).ConfigureAwait(false);
        await ObjectedAsync(subject, purpose, objecting: true, now, cancellationToken)
            .ConfigureAwait(false);
        await audit
            .RecordedAsync(Objected, context.Acting, subject, now, Named(purpose, version, mechanism), cancellationToken)
            .ConfigureAwait(false);
        await work.CommitAsync(cancellationToken).ConfigureAwait(false);

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

        IReadOnlyList<ObjectionRecord> held = await consents
            .ObjectionsAsync(subject, cancellationToken)
            .ConfigureAwait(false);

        if (Of(held, purpose) is not ObjectionRecord objection)
        {
            return Result.Failure(Error.From(ErrorCodes.Denied));
        }

        if (objection.WithdrawnAt is not null)
        {
            return Result.Success();
        }

        DateTimeOffset now = time.GetUtcNow();

        await work.BeginAsync(cancellationToken).ConfigureAwait(false);
        await consents
            .RecordAsync(subject, objection with { WithdrawnAt = now }, cancellationToken)
            .ConfigureAwait(false);
        await ObjectedAsync(subject, purpose, objecting: false, now, cancellationToken)
            .ConfigureAwait(false);
        await audit
            .RecordedAsync(
                Resumed,
                context.Acting,
                subject,
                now,
                Named(purpose, objection.NoticeVersion, objection.Mechanism),
                cancellationToken)
            .ConfigureAwait(false);
        await work.CommitAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }

    private static ConsentRecord? Of(IReadOnlyList<ConsentRecord> held, string purpose) =>
        held.LastOrDefault(record => string.Equals(record.Purpose, purpose, StringComparison.Ordinal));

    private static ObjectionRecord? Of(IReadOnlyList<ObjectionRecord> held, string purpose) =>
        held.LastOrDefault(record => string.Equals(record.Purpose, purpose, StringComparison.Ordinal));

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

    private async ValueTask AnnouncedAsync(
        SubjectId subject,
        string purpose,
        ConsentChange change,
        DateTimeOffset at,
        CancellationToken cancellationToken) =>
        await events
            .PublishAsync(
                new ConsentChanged(at, Key(subject, purpose, change.ToString(), at), purpose, change)
                {
                    Subject = subject,
                },
                cancellationToken)
            .ConfigureAwait(false);

    private async ValueTask ObjectedAsync(
        SubjectId subject,
        string purpose,
        bool objecting,
        DateTimeOffset at,
        CancellationToken cancellationToken) =>
        await events
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
            .ConfigureAwait(false);
}
