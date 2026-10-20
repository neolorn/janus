using System;
using System.Collections.Generic;
using System.Net.Mime;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Hosting.Bff;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Janus.Hosting.Privacy;

/// <summary>
/// The compliance-text endpoints of chapter 09 section 8a: publishing a version of the
/// privacy notice or of any other document, and attaching a translation to one.
/// </summary>
/// <remarks>
/// Implements PRIV-CONS-005, PRIV-CONS-006, PRIV-CONS-007, LIB-API-005 and
/// CONV-DESIGN-006. Each is one line to <see cref="ILegalDocuments"/>, which judges the
/// permission and refuses a version without its governing text. A document is named by
/// <see cref="DocumentName"/>, bound from the route, so a name outside its rule is
/// answered before the body is read (INT-SMS-003, chapter 09 section 8a).
/// </remarks>
internal static class PublicationEndpoints
{
    private static readonly DocumentName Notice = DocumentName.Parse("privacy-notice");

    private static readonly IResult Nothing = TypedResults.NoContent();

    /// <summary>
    /// Mounts them.
    /// </summary>
    /// <param name="endpoints">Where the host is mounting the library.</param>
    /// <returns>The builder, so the caller can go on.</returns>
    /// <exception cref="ArgumentNullException">The route builder is absent.</exception>
    public static IEndpointRouteBuilder MapPublication(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        _ = SessionRequired.On(endpoints.MapPost("/admin/notices", PublishNoticeAsync))
            .Declares(EndpointDeclaration
                .Answering(
                    ErrorCodes.Denied, ErrorCodes.Restricted, ErrorCodes.NoticeGoverningTextMissing))
            .Accepts<PublicationBody>(MediaTypeNames.Application.Json)
            .Produces<DocumentVersionView>();
        _ = SessionRequired.On(endpoints.MapPost("/admin/documents/{document}/versions", PublishAsync))
            .Declares(EndpointDeclaration
                .Answering(
                    ErrorCodes.RequestMalformed, ErrorCodes.Denied, ErrorCodes.Restricted,
                    ErrorCodes.NoticeGoverningTextMissing)
                .Binding<DocumentName>("document"))
            .Accepts<PublicationBody>(MediaTypeNames.Application.Json)
            .Produces<DocumentVersionView>();
        _ = SessionRequired.On(endpoints.MapPut(
            "/admin/documents/{document}/versions/{version}/translations/{language}",
            TranslateAsync))
            .Declares(EndpointDeclaration
                .Answering(
                    ErrorCodes.RequestMalformed, ErrorCodes.Denied, ErrorCodes.Restricted,
                    ErrorCodes.DocumentNotFound)
                .Binding<DocumentName>("document"))
            .Accepts<TranslationBody>(MediaTypeNames.Application.Json)
            .Produces(StatusCodes.Status204NoContent);

        return endpoints;
    }

    private static Task<IResult> PublishNoticeAsync(
        ILegalDocuments documents,
        RequestSession browser,
        HttpContext context,
        CancellationToken cancellationToken) =>
        PublishAsync(documents, browser, Notice, context, cancellationToken);

    private static async Task<IResult> PublishAsync(
        ILegalDocuments documents,
        RequestSession browser,
        DocumentName document,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(documents);
        ArgumentNullException.ThrowIfNull(browser);
        ArgumentNullException.ThrowIfNull(context);

        PublicationBody body = await JsonBody
            .ReadAsync<PublicationBody>(context.Request, cancellationToken)
            .ConfigureAwait(false);

        if (body.Material is not bool material)
        {
            return Answers.Malformed("material");
        }

        if (Translations(body.Translations) is not List<DocumentTranslation> translations)
        {
            return Answers.Malformed("translations");
        }

        // PRIV-CONS-006: a version without its governing text is the service's to
        // refuse, since the refusal is also raised as an alert.
        return Answers.Of(
            await documents
                .PublishAsync(
                    browser.Asking,
                    new DocumentPublication(
                        document,
                        body.Text ?? string.Empty,
                        body.GoverningLanguage,
                        translations,
                        material),
                    cancellationToken)
                .ConfigureAwait(false),
            Published);
    }

    private static async Task<IResult> TranslateAsync(
        ILegalDocuments documents,
        RequestSession browser,
        DocumentName document,
        string version,
        string language,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(documents);
        ArgumentNullException.ThrowIfNull(browser);
        ArgumentNullException.ThrowIfNull(context);

        TranslationBody body = await JsonBody
            .ReadAsync<TranslationBody>(context.Request, cancellationToken)
            .ConfigureAwait(false);

        if (body.Text is not { } text || string.IsNullOrWhiteSpace(text))
        {
            return Answers.Malformed("text");
        }

        return Answers.Of(
            await documents
                .TranslateAsync(
                    browser.Asking,
                    document,
                    version,
                    new DocumentTranslation(language, text),
                    cancellationToken)
                .ConfigureAwait(false),
            Nothing);
    }

    private static IResult Published(DocumentVersion version) =>
        TypedResults.Json(
            DocumentVersionView.Of(version),
            PrivacyJson.Default.DocumentVersionView,
            contentType: null,
            StatusCodes.Status200OK);

    // Each translation names its language and carries text, or the list is unreadable.
    private static List<DocumentTranslation>? Translations(IReadOnlyList<TranslationBody>? submitted)
    {
        var translations = new List<DocumentTranslation>(submitted?.Count ?? 0);

        foreach (TranslationBody? translation in submitted ?? [])
        {
            if (translation is not { Language: { } language, Text: { } text }
                || string.IsNullOrWhiteSpace(language)
                || string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            translations.Add(new DocumentTranslation(language, text));
        }

        return translations;
    }
}
