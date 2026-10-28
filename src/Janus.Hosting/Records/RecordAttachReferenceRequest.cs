namespace Janus.Hosting.Records;

/// <summary>
/// The body of <c>POST /account/records/attach/reference</c>.
/// </summary>
/// <param name="Reference">The reference the person holds from the collection.</param>
/// <remarks>Implements REC-ATT-001, chapter 09 section 7a.</remarks>
internal sealed record RecordAttachReferenceRequest(string? Reference);
