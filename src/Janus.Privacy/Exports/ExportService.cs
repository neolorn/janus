using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Core.Configuration;
using Janus.Privacy.Consents;
using Janus.Privacy.Outbox;

namespace Janus.Privacy.Exports;

/// <summary>
/// The one export routine. What the subject may have of what is held about them is
/// assembled once, and the two formats of PRIV-RIGHT-003 are two arrangements of what
/// this returns.
/// </summary>
/// <param name="source">Where the parts the other areas hold are read.</param>
/// <param name="ledger">Where the exports already taken are counted.</param>
/// <param name="consents">Where the consent and objection records are.</param>
/// <param name="stepUp">The gate the account's own assurance is asked at.</param>
/// <param name="outbox">Where the hosts are told to assemble their own half.</param>
/// <param name="audit">Where the export is written down.</param>
/// <param name="configuration">Where the rate limit is read.</param>
/// <param name="work">The one transaction the export is counted in.</param>
/// <param name="time">The clock the deployment runs on.</param>
/// <remarks>
/// Implements PRIV-RIGHT-003, LIB-API-005, D-086 and D-141. One routine and not two:
/// two routines are two answers to the same legal question, and the second one is the
/// one nobody maintains.
/// </remarks>
internal sealed class ExportService(
    IExportSource source,
    IExportLedger ledger,
    IConsentStore consents,
    IStepUpGate stepUp,
    IOutboxStore outbox,
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

        DateTimeOffset now = time.GetUtcNow();

        if (await SpentAsync(subject, now, cancellationToken).ConfigureAwait(false)
            is DateTimeOffset retryAt)
        {
            return Result.Failure<SubjectExport>(Error.From(
                ErrorCodes.Throttled,
                "retryAt",
                JsonSerializer.SerializeToElement(retryAt)));
        }

        var export = new SubjectExport(
            subject,
            now,
            await SectionsAsync(subject, cancellationToken).ConfigureAwait(false));

        await work.BeginAsync(cancellationToken).ConfigureAwait(false);
        await ledger.RecordAsync(subject, now, cancellationToken).ConfigureAwait(false);

        // PRIV-RIGHT-005b: the host holds the half the library cannot produce, and is
        // told in the transaction that counted the export against the rate limit.
        await outbox
            .AddAsync(
                Delivery.Of(subject, SubjectEventKind.ExportRequested, now),
                cancellationToken)
            .ConfigureAwait(false);

        await audit
            .RecordedAsync(Assembled, subject, subject, now, Named(export), cancellationToken)
            .ConfigureAwait(false);
        await work.CommitAsync(cancellationToken).ConfigureAwait(false);

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
        var values = new Dictionary<string, string>(capacity: 6, StringComparer.Ordinal)
        {
            ["purpose"] = record.Purpose,
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
        var values = new Dictionary<string, string>(capacity: 5, StringComparer.Ordinal)
        {
            ["purpose"] = record.Purpose,
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

    private async ValueTask<IReadOnlyList<ExportSection>> SectionsAsync(
        SubjectId subject,
        CancellationToken cancellationToken)
    {
        List<ExportSection> sections =
        [
            .. await source.SectionsAsync(subject, cancellationToken).ConfigureAwait(false),
        ];

        sections.Add(Recorded(
            "consents",
            (await consents.ConsentsAsync(subject, cancellationToken).ConfigureAwait(false))
                .Select(Held)));

        sections.Add(Recorded(
            "objections",
            (await consents.ObjectionsAsync(subject, cancellationToken).ConfigureAwait(false))
                .Select(Standing)));

        return sections;
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
            .Match(read => read, _ => Settings.PrivacyExportRateLimit.Default);

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
