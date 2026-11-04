namespace Janus.Authentication.Search;

/// <summary>
/// Where one indexed value lies in the index's own character buffer.
/// </summary>
/// <param name="Start">The first character's position.</param>
/// <param name="Length">How many characters the value holds.</param>
internal readonly record struct TextRange(int Start, int Length);
