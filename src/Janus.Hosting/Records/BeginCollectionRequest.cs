namespace Janus.Hosting.Records;

/// <summary>
/// The body of <c>POST /records/collection</c>.
/// </summary>
/// <param name="Age">Whether the host's screen asks the age question first; required.</param>
/// <remarks>Implements REC-COL-001 and REC-AGE-001, chapter 09 section 7a.</remarks>
internal sealed record BeginCollectionRequest(bool? Age);
