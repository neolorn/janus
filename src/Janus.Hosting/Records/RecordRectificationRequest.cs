namespace Janus.Hosting.Records;

/// <summary>
/// The body of <c>PUT /records/access/{subject}</c>: what the record's fields become.
/// </summary>
/// <param name="Name">The name, or nothing to leave it.</param>
/// <param name="Language">The language, or nothing to leave it.</param>
/// <remarks>Implements REC-ACC-002, chapter 09 section 7a.</remarks>
internal sealed record RecordRectificationRequest(string? Name, string? Language);
