using System.Collections.Generic;

namespace Janus.Privacy.SubjectKeys;

/// <summary>
/// A rotation that retired the versions before it.
/// </summary>
/// <param name="Rotation">The rotation, retired.</param>
/// <param name="Retired">
/// The versions retired, which leave the application's key document at once, nothing
/// being wrapped under them any longer; keeping one for a backup taken under it is the
/// deployment's (D-225).
/// </param>
/// <remarks>Implements OPS-SEC-003 AC3.</remarks>
internal sealed record KeyRetirement(KeyRotationProgress Rotation, IReadOnlyList<int> Retired);
