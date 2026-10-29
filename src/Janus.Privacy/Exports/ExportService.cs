using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Core.Configuration;
using Janus.Privacy.Consents;

namespace Janus.Privacy.Exports;

/// <summary>
/// The one export routine. What the subject may have of what is held about them is
/// assembled once, and the two formats of PRIV-RIGHT-003 are two arrangements of what
/// this returns.
/// </summary>
/// <param name="source">Where the parts the other areas hold are read.</param>
/// <param name="contributors">The export contributors the host registered, which answer what it holds.</param>
/// <param name="names">The rule the names a contributor answers are held to.</param>
/// <param name="faults">Where what a contributor threw is kept.</param>
/// <param name="ledger">Where the exports already taken are counted.</param>
/// <param name="consents">Where the consent and objection records are.</param>
/// <param name="stepUp">The gate the account's own assurance is asked at.</param>
/// <param name="audit">Where the export is written down.</param>
/// <param name="configuration">Where the rate limit is read.</param>
/// <param name="work">The one transaction the export is counted in.</param>
/// <param name="time">The clock the deployment runs on.</param>
/// <remarks>
/// Implements PRIV-RIGHT-003, REC-ACC-002, LIB-API-005, D-086, D-141, D-203, D-206, D-211
/// and D-221. One routine and not two: two routines are two answers to the same legal
/// question, and the second one is the one nobody maintains. A person record's export is
/// the same routine over the record's own sections.
/// </remarks>
internal sealed class ExportService(
    IExportSource source,
    IEnumerable<IExportContributor> contributors,
    IExportNameRule names,
    IFaultEntry faults,
    IExportLedger ledger,
    IConsentStore consents,
    IStepUpGate stepUp,
    IPrivacyAudit audit,
    IConfigurationStore configuration,
    IUnitOfWork work,
    TimeProvider time) : IExports
{
    /// <summary>
    /// The window the rate limit counts over, which chapter 10 gives as a day.
    /// </summary>
    internal static readonly TimeSpan Window = TimeSpan.FromDays(1);

    private static readonly AuditAction Assembled = AuditActions.ExportAssembled;

    // Chapter 10 section 5.46 (D-221): a contributor's sections for a person record take
    // no name of an account's export, nor record or records, so a name it may answer
    // under is the same whichever kind of subject it answers for.
    private static readonly FrozenSet<string> RecordNames = FrozenSet.ToFrozenSet(
        [
            "account", "profile", "identifiers", "identifier-backup", "credentials",
            "recovery-codes", "devices", "preferences", "memberships",
            "membership-acknowledgements", "group-memberships", "grants", "assurance",
            "sessions", "consents", "objections", "records", "record",
        ],
        StringComparer.Ordinal);

    /// <inheritdoc/>
    public async ValueTask<Result<SubjectExport>> AssembleAsync(
        AccessContext context,
        SessionId session,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Effective is not SubjectId subject)
        {
            return Result.Failure<SubjectExport>(Error.From(ErrorCodes.Denied));
        }

        // D-141: a live session is not enough for a complete copy of everything held
        // about a person, so the gate stands at the account's reachable assurance.
        if ((await stepUp
                .RequireAsync(subject, session, StepUpAction.PrivacyExport, cancellationToken)
                .ConfigureAwait(false))
            .Match(() => (Error?)null, error => error) is Error closed)
        {
            return Result.Failure<SubjectExport>(closed);
        }

        return await CountedAsync(
                subject,
                subject,
                context.BreakGlassReason,
                source.SectionsAsync,
                source.RecordsAsync,
                FrozenSet<string>.Empty,
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// The export of a person record a record access session reaches: the record's own
    /// sections, then each contributor's for its subject, counted against the limit for
    /// the record. No step-up is asked, since the code the record was reached by is fresh
    /// (REC-ACC-002).
    /// </summary>
    /// <param name="record">The record's subject.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The export, or <c>auth.throttled</c>, <c>system.fault</c>.</returns>
    internal async ValueTask<Result<SubjectExport>> AssembleRecordAsync(
        SubjectId record,
        CancellationToken cancellationToken) =>
        await CountedAsync(
                record,
                record,
                breakGlassReason: null,
                source.RecordSectionsAsync,
                attached: null,
                RecordNames,
                cancellationToken)
            .ConfigureAwait(false);

    /// <summary>
    /// The export of a person record attached to the account asking: the record's own
    /// sections, then each contributor's for its subject, counted against the limit for
    /// the record, behind the step-up the account's own export asks (REC-ATT-002).
    /// </summary>
    /// <param name="context">The account asking, which the caller found the record attached to.</param>
    /// <param name="session">The session the request arrived on.</param>
    /// <param name="record">The record's subject.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// The export, or <c>auth.stepup.required</c>, <c>auth.throttled</c>,
    /// <c>system.fault</c>, <c>authz.denied</c>.
    /// </returns>
    /// <exception cref="ArgumentNullException">The context is absent.</exception>
    internal async ValueTask<Result<SubjectExport>> AssembleAttachedAsync(
        AccessContext context,
        SessionId session,
        SubjectId record,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Effective is not SubjectId account)
        {
            return Result.Failure<SubjectExport>(Error.From(ErrorCodes.Denied));
        }

        // Chapter 09 section 7a: under the account's session the record's export is a
        // step-up action, as the account's own is (AUTH-STEP-002a).
        if ((await stepUp
                .RequireAsync(account, session, StepUpAction.PrivacyExport, cancellationToken)
                .ConfigureAwait(false))
            .Match(() => (Error?)null, error => error) is Error closed)
        {
            return Result.Failure<SubjectExport>(closed);
        }

        return await CountedAsync(
                record,
                record,
                context.BreakGlassReason,
                source.RecordSectionsAsync,
                attached: null,
                RecordNames,
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// The export of a person record staff assemble to hand to the person who reaches it
    /// by no code: the record's own sections, then each contributor's for its subject,
    /// counted against the limit for the record and recorded with the staff member
    /// acting (REC-ADM-002). The caller has admitted the staff member and judged the
    /// step-up.
    /// </summary>
    /// <param name="acting">The staff member assembling it.</param>
    /// <param name="breakGlassReason">The break-glass reason, where the session is one.</param>
    /// <param name="record">The record's subject.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The export, or <c>auth.throttled</c>, <c>system.fault</c>.</returns>
    internal async ValueTask<Result<SubjectExport>> AssembleOnBehalfAsync(
        SubjectId acting,
        string? breakGlassReason,
        SubjectId record,
        CancellationToken cancellationToken) =>
        await CountedAsync(
                record,
                acting,
                breakGlassReason,
                source.RecordSectionsAsync,
                attached: null,
                RecordNames,
                cancellationToken)
            .ConfigureAwait(false);

    // D-086, D-203: the export counted against the subject's limit, its contributors
    // answered before it is, and recorded in the transaction that counts it.
    private async ValueTask<Result<SubjectExport>> CountedAsync(
        SubjectId subject,
        SubjectId acting,
        string? breakGlassReason,
        Func<SubjectId, CancellationToken, ValueTask<IReadOnlyList<ExportSection>>> own,
        Func<SubjectId, CancellationToken, ValueTask<ExportSection>>? attached,
        IReadOnlySet<string> reserved,
        CancellationToken cancellationToken)
    {
        DateTimeOffset now = time.GetUtcNow();

        if (await SpentAsync(subject, now, cancellationToken).ConfigureAwait(false)
            is DateTimeOffset retryAt)
        {
            return Result.Failure<SubjectExport>(Error.Throttled(retryAt));
        }

        // PRIV-RIGHT-003 (D-203): every contributor has answered before the export is
        // counted, so one that failed leaves the person's limit as it was.
        Result<IReadOnlyList<ExportSection>> assembled =
            await SectionsAsync(subject, own, attached, reserved, cancellationToken).ConfigureAwait(false);

        if (assembled.Match(_ => (Error?)null, error => error) is Error notAssembled)
        {
            return Result.Failure<SubjectExport>(notAssembled);
        }

        var export = new SubjectExport(subject, now, assembled.Match(sections => sections, _ => []));

        if ((await work.BeginAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(_ => null, error => error) is Error notBegun)
        {
            return Result.Failure<SubjectExport>(notBegun);
        }

        // D-166 X3: the window is counted again with the subject's exports held, so
        // exports at the same moment are each counted against the ones before them.
        await ledger.HoldAsync(subject, cancellationToken).ConfigureAwait(false);

        if (await SpentAsync(subject, now, cancellationToken).ConfigureAwait(false)
            is DateTimeOffset spentUntil)
        {
            await work.RollbackAsync().ConfigureAwait(false);

            return Result.Failure<SubjectExport>(Error.Throttled(spentUntil));
        }

        await ledger.RecordAsync(subject, now, cancellationToken).ConfigureAwait(false);

        await audit
            .RecordedAsync(Assembled, acting, breakGlassReason, subject, now, Named(export), cancellationToken)
            .ConfigureAwait(false);

        if ((await work.CommitAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(() => null, error => error) is Error notCommitted)
        {
            return Result.Failure<SubjectExport>(notCommitted);
        }

        return Result.Success(export);
    }

    // PRIV-RET-004: the entry says what was assembled and never what was in it.
    private static Dictionary<string, JsonElement> Named(SubjectExport export) =>
        new(capacity: 2, StringComparer.Ordinal)
        {
            ["sections"] = JsonSerializer.SerializeToElement(
                export.Sections.Select(section => section.Name).ToArray()),
            ["records"] = JsonSerializer.SerializeToElement(
                export.Sections.Sum(section => section.Records.Count)),
        };

    private static ExportSection Recorded(
        string name,
        IEnumerable<IReadOnlyDictionary<string, string>> records) =>
        new(name, [.. records.Select(values => new ExportRecord(values))]);

    private static string Moment(DateTimeOffset at) =>
        at.ToString("O", CultureInfo.InvariantCulture);

    private static Dictionary<string, string> Held(ConsentRecord record)
    {
        var values = new Dictionary<string, string>(capacity: 7, StringComparer.Ordinal)
        {
            ["purpose"] = record.Purpose,
            ["document"] = record.Document,
            ["noticeVersion"] = record.NoticeVersion,
            ["mechanism"] = record.Mechanism.ToString(),
            ["grantedAt"] = Moment(record.GrantedAt),
        };

        if (record.WithdrawnAt is DateTimeOffset withdrawn)
        {
            values["withdrawnAt"] = Moment(withdrawn);
        }

        if (record.SupersededAt is DateTimeOffset superseded)
        {
            values["supersededAt"] = Moment(superseded);
        }

        return values;
    }

    private static Dictionary<string, string> Standing(ObjectionRecord record)
    {
        var values = new Dictionary<string, string>(capacity: 6, StringComparer.Ordinal)
        {
            ["purpose"] = record.Purpose,
            ["document"] = record.Document,
            ["noticeVersion"] = record.NoticeVersion,
            ["mechanism"] = record.Mechanism.ToString(),
            ["recordedAt"] = Moment(record.RecordedAt),
        };

        if (record.WithdrawnAt is DateTimeOffset withdrawn)
        {
            values["withdrawnAt"] = Moment(withdrawn);
        }

        return values;
    }

    private static Error Faulted(IExportContributor contributor) =>
        Error.From(ErrorCodes.SystemFault, "contributor", JsonSerializer.SerializeToElement(contributor.Name));

    // A contributor that throws has failed as one that answered a failure has: the
    // export names it to the log and is refused, never returned short of what it holds.
    // What it threw is kept by its type and frames beside the refusal, and never in the
    // error, which an in-process caller could pass on (D-211).
    private async ValueTask<Result<IReadOnlyList<ExportSection>>> AnsweredAsync(
        IExportContributor contributor,
        SubjectId subject,
        CancellationToken cancellationToken)
    {
        try
        {
            return (await contributor.SectionsAsync(subject, cancellationToken).ConfigureAwait(false))
                .Match(
                    held => Result.Success(held),
                    _ => Result.Failure<IReadOnlyList<ExportSection>>(Faulted(contributor)));
        }
        catch (Exception fault) when (fault is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            faults.Keep(contributor.Name, fault);

            return Result.Failure<IReadOnlyList<ExportSection>>(Faulted(contributor));
        }
    }

    // PRIV-RIGHT-003 (D-206): whether a contributor's answer may be taken, judged whole
    // before any of it is. A section under a name already taken, the library's, another
    // contributor's or another of the same answer, would be read as that one by the
    // frontend's labels and by the reader, and a section or value named outside the rule
    // could make two keys of the machine format coincide. The names of the answer join
    // those taken.
    private bool Kept(IReadOnlyList<ExportSection> answered, HashSet<string> taken)
    {
        foreach (ExportSection section in answered)
        {
            if (section is null
                || section.Records is null
                || !names.Holds(section.Name)
                || !taken.Add(section.Name))
            {
                return false;
            }

            foreach (ExportRecord record in section.Records)
            {
                if (record is null || record.Values is null || !record.Values.Keys.All(names.Holds))
                {
                    return false;
                }
            }
        }

        return true;
    }

    // PRIV-RIGHT-003 (D-203): the library's sections, then each contributor's in the
    // order of the contributors' names, asked outside any unit of work. A contributor
    // takes no name the library's sections take, nor one the export reserves.
    private async ValueTask<Result<IReadOnlyList<ExportSection>>> SectionsAsync(
        SubjectId subject,
        Func<SubjectId, CancellationToken, ValueTask<IReadOnlyList<ExportSection>>> own,
        Func<SubjectId, CancellationToken, ValueTask<ExportSection>>? attached,
        IReadOnlySet<string> reserved,
        CancellationToken cancellationToken)
    {
        List<ExportSection> sections = [.. await own(subject, cancellationToken).ConfigureAwait(false)];

        sections.Add(Recorded(
            "consents",
            (await consents.ConsentsAsync(subject, cancellationToken).ConfigureAwait(false))
                .Select(Held)));

        sections.Add(Recorded(
            "objections",
            (await consents.ObjectionsAsync(subject, cancellationToken).ConfigureAwait(false))
                .Select(Standing)));

        // Chapter 10 section 5.46 (D-221): an account's export names each record attached
        // to it after its own objections.
        if (attached is not null)
        {
            sections.Add(await attached(subject, cancellationToken).ConfigureAwait(false));
        }

        var taken = new HashSet<string>(sections.Select(section => section.Name).Concat(reserved), StringComparer.Ordinal);

        foreach (IExportContributor contributor in contributors.OrderBy(each => each.Name, StringComparer.Ordinal))
        {
            IReadOnlyList<ExportSection>? answered = (await AnsweredAsync(contributor, subject, cancellationToken)
                    .ConfigureAwait(false))
                .Match<IReadOnlyList<ExportSection>?>(held => held, _ => null);

            if (answered is null || !Kept(answered, taken))
            {
                return Result.Failure<IReadOnlyList<ExportSection>>(Faulted(contributor));
            }

            sections.AddRange(answered);
        }

        return Result.Success<IReadOnlyList<ExportSection>>(sections);
    }

    // D-086: the window rolls, so the limit cannot be spent twice across a boundary
    // that a calendar reset would put in the middle of one sitting.
    private async ValueTask<DateTimeOffset?> SpentAsync(
        SubjectId subject,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        int limit = (await configuration
                .ReadAsync(Settings.PrivacyExportRateLimit, cancellationToken).ConfigureAwait(false))
            .Match(read => read, error => throw new InvalidOperationException(error.Code.ToString()));

        // A deployment that set the limit to nothing has closed the door, and the
        // answer is still a refusal with a time on it rather than an index fault.
        if (limit <= 0)
        {
            return now + Window;
        }

        IReadOnlyList<DateTimeOffset> taken = await ledger
            .SinceAsync(subject, now - Window, cancellationToken)
            .ConfigureAwait(false);

        return taken.Count < limit ? null : taken[^limit] + Window;
    }
}
