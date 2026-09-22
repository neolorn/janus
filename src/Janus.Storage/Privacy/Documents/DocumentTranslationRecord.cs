namespace Janus.Storage.Privacy.Documents;

/// <summary>
/// The <c>legal_document_translations</c> row.
/// </summary>
/// <remarks>
/// Implements PRIV-CONS-005 and PRIV-CONS-006. A translation is corrected in place
/// because correcting one changes nothing about what was shown as authoritative.
/// </remarks>
internal sealed class DocumentTranslationRecord
{
    /// <summary>
    /// The <c>document</c> column.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// The <c>version</c> column.
    /// </summary>
    public string Version { get; set; } = string.Empty;

    /// <summary>
    /// The <c>language</c> column: what this text is written in.
    /// </summary>
    public string Language { get; set; } = string.Empty;

    /// <summary>
    /// The <c>translated_text</c> column.
    /// </summary>
    public string TranslatedText { get; set; } = string.Empty;
}
