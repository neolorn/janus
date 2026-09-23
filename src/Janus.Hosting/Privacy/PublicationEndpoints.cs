using System;
using System.Collections.Generic;
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
/// permission and refuses a version without its governing text.
/// </remarks>
internal static class PublicationEndpoints
{
    private const string Notice = "privacy-notice";

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

        _ = SessionRequired.On(endpoints.MapPost("/admin/notices", PublishNoticeAsync));
        _ = SessionRequired.On(endpoints.MapPost("/admin/documents/{document}/versions", PublishAsync));
        _ = SessionRequired.On(endpoints.MapPut(
            "/admin/documents/{document}/versions/{version}/translations/{language}",
            TranslateAsync));

        return endpoints;
    }

    private static Task<IResult> PublishNoticeAsync(
        PublicationBody body,
        ILegalDocuments documents,
        RequestSession browser,
        CancellationToken cancellationToken) =>
        PublishAsync(body, documents, browser, Notice, cancellationToken);

    private static async Task<IResult> PublishAsync(
        PublicationBody body,
        ILegalDocuments documents,
        RequestSession browser,
        string document,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(body);
        ArgumentNullException.ThrowIfNull(documents);
        ArgumentNullException.ThrowIfNull(browser);

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
                    AccessContext.Of(browser.Required.Subject),
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
        TranslationBody body,
        ILegalDocuments documents,
        RequestSession browser,
        string document,
        string version,
        string language,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(body);
        ArgumentNullException.ThrowIfNull(documents);
        ArgumentNullException.ThrowIfNull(browser);

        if (body.Text is not { } text || string.IsNullOrWhiteSpace(text))
        {
            return Answers.Malformed("text");
        }

        return Answers.Of(
            await documents
                .TranslateAsync(
                    AccessContext.Of(browser.Required.Subject),
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
