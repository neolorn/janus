namespace Janus.Hosting.Records;

/// <summary>
/// The body of <c>POST /records/access/reference</c>.
/// </summary>
/// <param name="Reference">The reference the person holds from the collection.</param>
/// <remarks>Implements REC-ACC-001, chapter 09 section 7a.</remarks>
internal sealed record RecordAccessReferenceRequest(string? Reference);
