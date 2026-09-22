using System.Collections.Generic;

namespace Janus.Core;

/// <summary>
/// What publishing a version of a legal document carries.
/// </summary>
/// <param name="DocumentName">Which document is being published.</param>
/// <param name="Text">The governing-language text, without which nothing publishes.</param>
/// <param name="GoverningLanguage">
/// The language this version binds in, or nothing to take
/// <c>legal.governinglanguage</c>.
/// </param>
/// <param name="Translations">The translations to attach at once, which may be none.</param>
/// <param name="Material">
/// Whether the change is material. A material version supersedes every live consent
/// recorded against an earlier version of the same document; the person publishing
/// decides, and the audit record carries the answer.
/// </param>
/// <remarks>
/// Implements PRIV-CONS-005, PRIV-CONS-006, PRIV-CONS-007 and chapter 09 section 8a.
/// </remarks>
public sealed record DocumentPublication(
    string DocumentName,
    string Text,
    string? GoverningLanguage,
    IReadOnlyList<DocumentTranslation> Translations,
    bool Material);
