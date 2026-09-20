using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Hosting.Bff;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Janus.Hosting.Privacy;

/// <summary>
/// The compliance-text endpoints of chapter 09 section 7.
/// </summary>
/// <remarks>
/// Implements CONV-DESIGN-006, API-CONV-001, LIB-API-005, PRIV-CONS-005 and
/// PRIV-CONS-006. Both are public and unauthenticated: a document a person is
/// governed by cannot be behind a sign-in.
/// </remarks>
internal static class PrivacyEndpoints
{
    private const string Notice = "privacy-notice";

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

        return endpoints;
    }

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
            ? TypedResults.BadRequest()
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
}
