using System;
using System.Collections.Generic;

namespace Janus.Core;

/// <summary>
/// One published version of a legal document: the text that binds, the language it
/// binds in, and the translations attached to it.
/// </summary>
/// <param name="DocumentName">Which document it is a version of.</param>
/// <param name="Version">The version.</param>
/// <param name="GoverningLanguage">The one language this version binds in.</param>
/// <param name="Text">The governing-language text.</param>
/// <param name="Translations">The translations attached to it, which may be none.</param>
/// <param name="PublishedAt">When it was published.</param>
/// <remarks>
/// Implements PRIV-CONS-005, PRIV-CONS-006 and chapter 09 section 7. The governing
/// text travels with the translations, so a screen shows either without changing the
/// interface language and no translation is ever served as if it governed.
/// </remarks>
public sealed record DocumentVersion(
    string DocumentName,
    string Version,
    string GoverningLanguage,
    string Text,
    IReadOnlyList<DocumentTranslation> Translations,
    DateTimeOffset PublishedAt);
