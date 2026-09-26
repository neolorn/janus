using System.Collections.Generic;

namespace Janus.Privacy.SubjectKeys;

/// <summary>
/// A rotation that retired the versions before it.
/// </summary>
/// <param name="Rotation">The rotation, retired.</param>
/// <param name="Retired">
/// The versions retired, which the operator removes from the secrets manager: nothing
/// is wrapped under them any longer.
/// </param>
/// <remarks>Implements OPS-SEC-003 AC3.</remarks>
internal sealed record KeyRetirement(KeyRotationProgress Rotation, IReadOnlyList<int> Retired);
