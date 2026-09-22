using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Hosting.Bff;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;

namespace Janus.Hosting.Privacy;

/// <summary>
/// The compliance-text endpoints of chapter 09 section 7.
/// </summary>
/// <remarks>
/// Implements CONV-DESIGN-006, API-CONV-001, LIB-API-005, PRIV-CONS-005,
/// PRIV-CONS-006, PRIV-CONS-008 and PRIV-CONS-011. The two document reads are public
/// and unauthenticated: a document a person is governed by cannot be behind a
/// sign-in. The rest are the subject's own dashboard and answer nobody else.
/// </remarks>
internal static class PrivacyEndpoints
{
    private const string Notice = "privacy-notice";

    private static readonly IResult Nothing = TypedResults.NoContent();

    /// <summary>
    /// Mounts them.
    /// </summary>
    /// <param name="endpoints">Where the host is mounting the library.</param>
    /// <returns>The builder, so the caller can go on.</returns>
    /// <exception cref="ArgumentNullException">The route builder is absent.</exception>
    public static IEndpointRouteBuilder MapPrivacy(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        RouteGroupBuilder group = endpoints.MapGroup("/privacy");

        _ = group.MapGet("/notice", NoticeAsync);
        _ = group.MapGet("/documents/{document}", DocumentAsync);

        _ = group.MapGet("/consents", ConsentsAsync);
        _ = group.MapPost("/consents/{purpose}/grant", GrantAsync);
        _ = group.MapPost("/consents/{purpose}/withdraw", WithdrawAsync);

        _ = group.MapGet("/objections", ObjectionsAsync);
        _ = group.MapPost("/objections/{purpose}", ObjectAsync);
        _ = group.MapDelete("/objections/{purpose}", WithdrawObjectionAsync);

        _ = group.MapPost("/requests", SubmitAsync);
        _ = group.MapGet("/export", ExportAsync);

        _ = endpoints.MapGet("/admin/ropa", RegisterAsync);
        _ = endpoints.MapPut("/admin/compliance/assessments", AssessmentsAsync);

        RouteGroupBuilder queue = endpoints.MapGroup("/admin/privacy/requests");

        _ = queue.MapGet("/", QueueAsync);
        _ = queue.MapPost("/", EnterAsync);
        _ = queue.MapPost("/{request:guid}/fulfil", FulfilAsync);
        _ = queue.MapPost("/{request:guid}/refuse", RefuseAsync);

        return endpoints;
    }

    private static async Task<IResult> ConsentsAsync(
        IConsents consents,
        RequestSession browser,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(consents);

        return Asking(browser) is not AccessContext holder
            ? Nobody()
            : Answers.Of(
                await consents.ReadAsync(holder, cancellationToken).ConfigureAwait(false),
                Held);
    }

    private static async Task<IResult> GrantAsync(
        IConsents consents,
        RequestSession browser,
        string purpose,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(consents);

        return Asking(browser) is not AccessContext holder
            ? Nobody()
            : Answers.Of(
                await consents
                    .GrantAsync(holder, purpose, ConsentMechanism.Dashboard, cancellationToken)
                    .ConfigureAwait(false),
                Nothing);
    }

    private static async Task<IResult> WithdrawAsync(
        IConsents consents,
        RequestSession browser,
        string purpose,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(consents);

        return Asking(browser) is not AccessContext holder
            ? Nobody()
            : Answers.Of(
                await consents.WithdrawAsync(holder, purpose, cancellationToken).ConfigureAwait(false),
                Nothing);
    }

    private static async Task<IResult> ObjectionsAsync(
        IConsents consents,
        RequestSession browser,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(consents);

        return Asking(browser) is not AccessContext holder
            ? Nobody()
            : Answers.Of(
                await consents.ObjectionsAsync(holder, cancellationToken).ConfigureAwait(false),
                Standing);
    }

    private static async Task<IResult> ObjectAsync(
        IConsents consents,
        RequestSession browser,
        string purpose,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(consents);

        return Asking(browser) is not AccessContext holder
            ? Nobody()
            : Answers.Of(
                await consents
                    .ObjectAsync(holder, purpose, ConsentMechanism.Dashboard, cancellationToken)
                    .ConfigureAwait(false),
                Nothing);
    }

    private static async Task<IResult> WithdrawObjectionAsync(
        IConsents consents,
        RequestSession browser,
        string purpose,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(consents);

        return Asking(browser) is not AccessContext holder
            ? Nobody()
            : Answers.Of(
                await consents
                    .WithdrawObjectionAsync(holder, purpose, cancellationToken)
                    .ConfigureAwait(false),
                Nothing);
    }

    // PRIV-RIGHT-001: the subject asks for themselves, and the receipt tells them
    // when the decision is due, which is the one thing the statute gives them.
    private static async Task<IResult> SubmitAsync(
        PrivacyRequestBody body,
        IPrivacyRequests requests,
        RequestSession browser,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(body);
        ArgumentNullException.ThrowIfNull(requests);

        if (Asked(body.Type) is not PrivacyRequestType type)
        {
            return Answers.Malformed("type");
        }

        return Asking(browser) is not AccessContext holder
            ? Nobody()
            : Answers.Of(
                await requests
                    .SubmitAsync(holder, type, body.Detail ?? string.Empty, cancellationToken)
                    .ConfigureAwait(false),
                Receipted);
    }

    // D-054: one routine and two arrangements of what it returns. A format the
    // chapter does not name is a malformed request and never a silent choice of one.
    private static async Task<IResult> ExportAsync(
        IExports exports,
        RequestSession browser,
        string? format,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(exports);

        if (Arranged(format) is not Func<SubjectExport, IResult> arrangement)
        {
            return Answers.Malformed("format");
        }

        return Asking(browser) is not AccessContext holder || browser.Live is null
            ? Nobody()
            : Answers.Of(
                await exports
                    .AssembleAsync(holder, browser.Live.Id, cancellationToken)
                    .ConfigureAwait(false),
                arrangement);
    }

    private static Func<SubjectExport, IResult>? Arranged(string? format) => format switch
    {
        "human" => Readable,
        "machine" => Portable,
        _ => null,
    };

    private static JsonHttpResult<ExportView> Readable(SubjectExport export) =>
        TypedResults.Json(
            new ExportView(
                export.Subject.Value.ToString(),
                export.AssembledAt,
                [
                    .. export.Sections.Select(section => new ExportSectionView(
                        section.Name,
                        [.. section.Records.Select(record => record.Values)])),
                ]),
            PrivacyJson.Default.ExportView,
            contentType: null,
            StatusCodes.Status200OK);

    // PRIV-RIGHT-003 AC2: the name is the section, the record's place in it and the
    // field, so a reader that met one export can read the next one.
    private static JsonHttpResult<PortableExportView> Portable(SubjectExport export)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (ExportSection section in export.Sections)
        {
            for (int index = 0; index < section.Records.Count; index++)
            {
                foreach (KeyValuePair<string, string> value in section.Records[index].Values)
                {
                    values[string.Create(
                        CultureInfo.InvariantCulture,
                        $"{section.Name}.{index}.{value.Key}")] = value.Value;
                }
            }
        }

        return TypedResults.Json(
            new PortableExportView(export.Subject.Value.ToString(), export.AssembledAt, values),
            PrivacyJson.Default.PortableExportView,
            contentType: null,
            StatusCodes.Status200OK);
    }

    // PRIV-ROPA-001: the register is a query, so it is generated on the request and
    // never read from anything anyone maintains. The one format is the template's.
    private static async Task<IResult> RegisterAsync(
        IProcessingRecords records,
        RequestSession browser,
        string? format,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(records);

        if (format is not "template")
        {
            return Answers.Malformed("format");
        }

        return Asking(browser) is not AccessContext holder
            ? Nobody()
            : Answers.Of(
                await records.GenerateAsync(holder, cancellationToken).ConfigureAwait(false),
                Generated);
    }

    private static async Task<IResult> AssessmentsAsync(
        AssessmentsRequest request,
        IProcessingRecords records,
        RequestSession browser,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(records);

        return Asking(browser) is not AccessContext holder
            ? Nobody()
            : Answers.Of(
                await records
                    .DeclareAsync(
                        holder,
                        new ComplianceRecord(
                            request.DataOwner,
                            request.OrganisationalSecurityMeasures,
                            request.AssessmentLinks ?? []),
                        cancellationToken)
                    .ConfigureAwait(false),
                Nothing);
    }

    private static JsonHttpResult<ProcessingRegisterView> Generated(ProcessingRegister register) =>
        TypedResults.Json(
            ProcessingRegisterView.Of(register),
            PrivacyJson.Default.ProcessingRegisterView,
            contentType: null,
            StatusCodes.Status200OK);

    private static async Task<IResult> QueueAsync(
        IPrivacyRequests requests,
        RequestSession browser,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(requests);

        return Asking(browser) is not AccessContext holder
            ? Nobody()
            : Answers.Of(
                await requests.QueueAsync(holder, cancellationToken).ConfigureAwait(false),
                Queued);
    }

    // 09 section 8a, D-113: the human entering it records the channel, what they did
    // to confirm the requester is the subject, and the date it reached the company.
    private static async Task<IResult> EnterAsync(
        PrivacyEntryBody body,
        IPrivacyRequests requests,
        RequestSession browser,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(body);
        ArgumentNullException.ThrowIfNull(requests);

        if (Asked(body.Type) is not PrivacyRequestType type)
        {
            return Answers.Malformed("type");
        }

        if (!Guid.TryParse(body.Subject, out Guid subject))
        {
            return Answers.Malformed("subject");
        }

        if (!DateOnly.TryParseExact(
            body.ReceivedAt,
            "yyyy-MM-dd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out DateOnly receivedAt))
        {
            return Answers.Malformed("receivedAt");
        }

        if (body.Channel is not { Length: > 0 } channel)
        {
            return Answers.Malformed("channel");
        }

        if (body.IdentityConfirmation is not { Length: > 0 } confirmation)
        {
            return Answers.Malformed("identityConfirmation");
        }

        return Asking(browser) is not AccessContext holder
            ? Nobody()
            : Answers.Of(
                await requests
                    .EnterAsync(
                        holder,
                        new PrivacyRequestEntry(
                            new SubjectId(subject),
                            type,
                            body.Detail ?? string.Empty,
                            receivedAt,
                            channel,
                            confirmation),
                        cancellationToken)
                    .ConfigureAwait(false),
                Receipted);
    }

    private static async Task<IResult> FulfilAsync(
        IPrivacyRequests requests,
        RequestSession browser,
        Guid request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(requests);

        return Asking(browser) is not AccessContext holder
            ? Nobody()
            : Answers.Of(
                await requests
                    .FulfilAsync(holder, new PrivacyRequestId(request), cancellationToken)
                    .ConfigureAwait(false),
                Nothing);
    }

    private static async Task<IResult> RefuseAsync(
        PrivacyDecisionBody body,
        IPrivacyRequests requests,
        RequestSession browser,
        Guid request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(body);
        ArgumentNullException.ThrowIfNull(requests);

        if (body.Reason is not { Length: > 0 } reason)
        {
            return Answers.Malformed("reason");
        }

        return Asking(browser) is not AccessContext holder
            ? Nobody()
            : Answers.Of(
                await requests
                    .RefuseAsync(holder, new PrivacyRequestId(request), reason, cancellationToken)
                    .ConfigureAwait(false),
                Nothing);
    }

    // 10 section 5.12c gives the three spellings, and a body carrying anything else
    // is malformed rather than a request for something the library does not do.
    private static PrivacyRequestType? Asked(string? type) => type switch
    {
        "restriction" => PrivacyRequestType.Restriction,
        "rectification" => PrivacyRequestType.Rectification,
        "erasure" => PrivacyRequestType.Erasure,
        _ => null,
    };

    private static IResult Receipted(PrivacyRequestReceipt receipt) =>
        TypedResults.Json(
            new PrivacyReceiptView(
                receipt.RequestId.Value,
                receipt.ReceiptSentAt,
                receipt.DecisionDue),
            PrivacyJson.Default.PrivacyReceiptView,
            contentType: null,
            StatusCodes.Status202Accepted);

    private static IResult Queued(IReadOnlyList<PrivacyRequest> queue) =>
        TypedResults.Json(
            (IReadOnlyList<PrivacyRequestView>)
            [
                .. queue.Select(request => new PrivacyRequestView(
                    request.Id.Value,
                    request.Subject.Value,
                    request.Type,
                    request.Detail,
                    request.ReceivedAt,
                    request.CreatedAt,
                    request.ReceiptSentAt,
                    request.DecisionDue,
                    request.Status,
                    request.DecidedAt,
                    request.DecisionReason,
                    request.Channel,
                    request.IdentityConfirmation)),
            ],
            PrivacyJson.Default.IReadOnlyListPrivacyRequestView,
            contentType: null,
            StatusCodes.Status200OK);

    private static Task<IResult> NoticeAsync(
        ILegalDocuments documents,
        string? version,
        CancellationToken cancellationToken) =>
        ReadAsync(documents, Notice, version, cancellationToken);

    private static Task<IResult> DocumentAsync(
        ILegalDocuments documents,
        string document,
        string? version,
        CancellationToken cancellationToken) =>
        ReadAsync(documents, document, version, cancellationToken);

    private static async Task<IResult> ReadAsync(
        ILegalDocuments documents,
        string document,
        string? version,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(documents);

        return document is not { Length: > 0 }
            ? Answers.Malformed("document")
            : Answers.Of(
                await documents.ReadAsync(document, version, cancellationToken).ConfigureAwait(false),
                Published);
    }

    private static IResult Published(DocumentVersion version) =>
        TypedResults.Json(
            new DocumentVersionView(
                version.DocumentName,
                version.Version,
                version.GoverningLanguage,
                version.Text,
                [
                    .. version.Translations.Select(translation =>
                        new DocumentTranslationView(translation.Language, translation.Text)),
                ]),
            PrivacyJson.Default.DocumentVersionView,
            contentType: null,
            StatusCodes.Status200OK);

    private static IResult Held(IReadOnlyList<ConsentRecord> records) =>
        TypedResults.Json(
            (IReadOnlyList<ConsentView>)
            [
                .. records.Select(record => new ConsentView(
                    record.Purpose,
                    record.NoticeVersion,
                    record.Mechanism,
                    record.GrantedAt,
                    record.WithdrawnAt,
                    record.SupersededAt)),
            ],
            PrivacyJson.Default.IReadOnlyListConsentView,
            contentType: null,
            StatusCodes.Status200OK);

    private static IResult Standing(IReadOnlyList<ObjectionRecord> records) =>
        TypedResults.Json(
            (IReadOnlyList<ObjectionView>)
            [
                .. records.Select(record => new ObjectionView(
                    record.Purpose,
                    record.NoticeVersion,
                    record.Mechanism,
                    record.RecordedAt,
                    record.WithdrawnAt)),
            ],
            PrivacyJson.Default.IReadOnlyListObjectionView,
            contentType: null,
            StatusCodes.Status200OK);

    private static AccessContext? Asking(RequestSession browser)
    {
        ArgumentNullException.ThrowIfNull(browser);

        return browser.Context;
    }

    // API-CONV-003: nobody is asking, which is what 401 is for and what nothing else
    // is for.
    private static IResult Nobody() => Answers.Refused(ErrorCodes.SessionExpired);
}
