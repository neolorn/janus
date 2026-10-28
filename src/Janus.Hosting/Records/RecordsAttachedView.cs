namespace Janus.Hosting.Records;

/// <summary>
/// How many records an attachment attached.
/// </summary>
/// <param name="Attached">The count, 0 where none was.</param>
/// <remarks>Implements REC-ATT-001, chapter 09 section 7a.</remarks>
internal sealed record RecordsAttachedView(int Attached);
