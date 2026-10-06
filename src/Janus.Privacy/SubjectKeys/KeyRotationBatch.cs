namespace Janus.Privacy.SubjectKeys;

/// <summary>
/// One batch of the ordered pass over the rows of the subject-key table.
/// </summary>
/// <param name="Last">The last row the batch took, or nothing where none was left.</param>
/// <param name="Processed">
/// How many values the batch moved to the current version: subject keys re-wrapped, or
/// fingerprints computed again.
/// </param>
/// <remarks>Implements OPS-SEC-003.</remarks>
internal sealed record KeyRotationBatch(SubjectKeyId? Last, int Processed);
