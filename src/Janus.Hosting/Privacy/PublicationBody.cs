using System.Collections.Generic;

namespace Janus.Hosting.Privacy;

/// <summary>
/// What publishing a version of a legal document carries, as the request reads.
/// </summary>
/// <param name="Text">The governing-language text.</param>
/// <param name="GoverningLanguage">The language it binds in, or nothing for the default.</param>
/// <param name="Translations">The translations to attach at once.</param>
/// <param name="Material">Whether the change is material; required.</param>
/// <remarks>Implements chapter 09 section 8a, PRIV-CONS-005 and PRIV-CONS-007.</remarks>
internal sealed record PublicationBody(
    string? Text,
    string? GoverningLanguage,
    IReadOnlyList<TranslationBody>? Translations,
    bool? Material);
