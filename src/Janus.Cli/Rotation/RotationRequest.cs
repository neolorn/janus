using System;

namespace Janus.Cli.Rotation;

/// <summary>
/// What a rotation command is asked to do.
/// </summary>
/// <param name="Sealed">Whether the escrow copy is confirmed sealed, which retires the versions before it.</param>
/// <param name="Retention">
/// How long a backup is kept, which a retired version is kept beyond the rotation's
/// completion: <c>backup.retention</c>'s default, or the longer one the operator named.
/// </param>
internal sealed record RotationRequest(bool Sealed, TimeSpan Retention);
