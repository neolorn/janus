namespace Janus.Hosting.Privacy;

/// <summary>
/// One translation attached to a version.
/// </summary>
/// <param name="Language">What it is written in.</param>
/// <param name="Text">The text.</param>
/// <remarks>Implements PRIV-CONS-005 and PRIV-CONS-006.</remarks>
internal sealed record DocumentTranslationView(string Language, string Text);
