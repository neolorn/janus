using System;
using System.Collections.Generic;
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
