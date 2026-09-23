namespace Janus.Hosting.Privacy;

/// <summary>
/// One translation of a legal document version, as the request reads.
/// </summary>
/// <param name="Language">The language it is written in, where the route does not name it.</param>
/// <param name="Text">The text.</param>
/// <remarks>Implements chapter 09 section 8a and PRIV-CONS-006.</remarks>
internal sealed record TranslationBody(string? Language, string? Text);
