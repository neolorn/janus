using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Janus.Hosting.Privacy;

/// <summary>
/// One version of a legal document as chapter 09 section 7 gives it.
/// </summary>
/// <param name="DocumentName">Which document.</param>
/// <param name="Version">Which version.</param>
/// <param name="GoverningLanguage">The one language it binds in.</param>
/// <param name="Text">The governing-language text.</param>
/// <param name="Translations">The translations attached to it.</param>
/// <remarks>
/// Implements PRIV-CONS-005 and PRIV-CONS-006. The governing text is always returned
/// with the translations, so a screen can show either without changing the interface
/// language and no translation is served as if it governed. The wire name of the
/// first field is chapter 09's; the member is named apart from it because
/// AUTHZ-MODEL-001 keeps a host domain type name out of library source.
/// </remarks>
internal sealed record DocumentVersionView(
    [property: JsonPropertyName("document")] string DocumentName,
    string Version,
    string GoverningLanguage,
    string Text,
    IReadOnlyList<DocumentTranslationView> Translations);
