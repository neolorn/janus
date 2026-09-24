using Janus.Core;

namespace Janus.Privacy.SubjectKeys;

/// <summary>
/// One batch of the ordered pass over the subject keys.
/// </summary>
/// <param name="Last">The last subject the batch took, or nothing where none was left.</param>
/// <param name="ReWrapped">How many of the keys it took were re-wrapped.</param>
/// <remarks>Implements OPS-SEC-003.</remarks>
internal sealed record KeyRotationBatch(SubjectId? Last, int ReWrapped);
