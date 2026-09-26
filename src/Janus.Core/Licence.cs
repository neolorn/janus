using System;

namespace Janus.Core;

/// <summary>
/// One licence or permit whose expiry the system warns of.
/// </summary>
/// <param name="Id">What it is known by.</param>
/// <param name="Kind">Whether it is the licence or a permit.</param>
/// <param name="Name">What the operator calls it.</param>
/// <param name="ExpiresAt">When it lapses.</param>
/// <param name="RenewedAt">When it was last renewed, where it has been.</param>
/// <remarks>Implements OPS-MAINT-001 (D-153).</remarks>
public sealed record Licence(
    LicenceId Id,
    LicenceKind Kind,
    string Name,
    DateTimeOffset ExpiresAt,
    DateTimeOffset? RenewedAt);
