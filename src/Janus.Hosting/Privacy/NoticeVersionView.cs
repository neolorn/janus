using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using Janus.Core;

namespace Janus.Hosting.Privacy;

/// <summary>
/// One version of the privacy notice as chapter 09 <c>GET /privacy/notice</c> gives it:
/// a version of a legal document, with the cookies it lists.
/// </summary>
/// <param name="DocumentName">Which document.</param>
/// <param name="Version">Which version.</param>
/// <param name="GoverningLanguage">The one language it binds in.</param>
/// <param name="Text">The governing-language text.</param>
/// <param name="Translations">The translations attached to it.</param>
/// <param name="Cookies">The cookies the deployment set when the version was published.</param>
/// <remarks>
/// Implements PRIV-CONS-005, PRIV-CONS-006 and PRIV-CONS-006a (D-203). The notice alone
/// carries the cookies, which the notice shows beside its text, so a reader of an old
/// version sees what it listed. The wire name of the first field is chapter 09's, named
/// apart from it as <see cref="DocumentVersionView"/> names it.
/// </remarks>
internal sealed record NoticeVersionView(
    [property: JsonPropertyName("document")] string DocumentName,
    string Version,
    string GoverningLanguage,
    string Text,
    IReadOnlyList<DocumentTranslationView> Translations,
    IReadOnlyList<NoticeCookieView> Cookies)
{
    /// <summary>
    /// The view of a version of the notice.
    /// </summary>
    /// <param name="version">The version.</param>
    /// <returns>The view.</returns>
    /// <exception cref="ArgumentNullException">The version is absent.</exception>
    public static NoticeVersionView Of(DocumentVersion version)
    {
        ArgumentNullException.ThrowIfNull(version);

        return new NoticeVersionView(
            version.DocumentName,
            version.Version,
            version.GoverningLanguage,
            version.Text,
            [
                .. version.Translations.Select(translation =>
                    new DocumentTranslationView(translation.Language, translation.Text)),
            ],
            [.. version.Cookies.Select(NoticeCookieView.Of)]);
    }
}
