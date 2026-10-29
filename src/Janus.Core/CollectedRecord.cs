namespace Janus.Core;

/// <summary>
/// The person record a collection wrote or reused.
/// </summary>
/// <param name="Subject">The record's subject, which the host names as the data subject of its own records.</param>
/// <param name="Created">Whether the collection created the record rather than reusing one.</param>
/// <remarks>Implements REC-COL-002.</remarks>
public sealed record CollectedRecord(SubjectId Subject, bool Created);
