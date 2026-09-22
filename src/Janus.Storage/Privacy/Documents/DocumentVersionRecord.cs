using System;
using System.Collections.Generic;

namespace Janus.Storage.Privacy.Documents;

/// <summary>
/// The <c>legal_document_versions</c> row.
/// </summary>
/// <remarks>
/// Implements PRIV-CONS-005 and PRIV-CONS-006. A row is written once and never
/// edited: what a consent was given against has to still read as it read then.
/// </remarks>
internal sealed class DocumentVersionRecord
{
    /// <summary>
    /// The <c>document</c> column: which document this is a version of.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// The <c>version</c> column.
    /// </summary>
    public string Version { get; set; } = string.Empty;

    /// <summary>
    /// The <c>governing_language</c> column: the one language this version binds in.
    /// </summary>
    public string GoverningLanguage { get; set; } = string.Empty;

    /// <summary>
    /// The <c>governing_text</c> column: the text that binds.
    /// </summary>
    public string GoverningText { get; set; } = string.Empty;

    /// <summary>
    /// The <c>published_at</c> column.
    /// </summary>
    public DateTimeOffset PublishedAt { get; set; }

    /// <summary>
    /// The translations attached to this version.
    /// </summary>
    public ICollection<DocumentTranslationRecord> Translations { get; } = [];
}
