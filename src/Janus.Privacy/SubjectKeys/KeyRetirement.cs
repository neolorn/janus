using System.Collections.Generic;

namespace Janus.Privacy.SubjectKeys;

/// <summary>
/// A rotation that retired the versions before it.
/// </summary>
/// <param name="Rotation">The rotation, retired.</param>
/// <param name="Retired">
/// The versions retired, which the operator removes from the application's key document
/// at once, nothing being wrapped under them any longer, and from the envelope and the
/// secrets manager once every backup taken under them has expired.
/// </param>
/// <remarks>Implements OPS-SEC-003 AC3 and D-166 (317).</remarks>
internal sealed record KeyRetirement(KeyRotationProgress Rotation, IReadOnlyList<int> Retired);
