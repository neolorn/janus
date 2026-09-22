namespace Janus.Core;

/// <summary>
/// One document version rendered in another language.
/// </summary>
/// <param name="Language">The language it is written in.</param>
/// <param name="Text">The text.</param>
/// <remarks>
/// Implements PRIV-CONS-005 and PRIV-CONS-006. A translation is an aid to the reader
/// and never governs; where it and the governing text diverge, the governing text
/// governs.
/// </remarks>
public sealed record DocumentTranslation(string Language, string Text);
