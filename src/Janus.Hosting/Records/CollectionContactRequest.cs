namespace Janus.Hosting.Records;

/// <summary>
/// The body of <c>PUT /records/collection/email</c> and <c>PUT /records/collection/phone</c>.
/// </summary>
/// <param name="Value">The value entered.</param>
/// <remarks>Implements REC-COL-001, chapter 09 section 7a.</remarks>
internal sealed record CollectionContactRequest(string? Value);
