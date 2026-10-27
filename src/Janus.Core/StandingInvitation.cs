using System;
using System.Collections.Generic;

namespace Janus.Core;

/// <summary>
/// One invitation an organization issued that is neither acknowledged, revoked nor
/// expired.
/// </summary>
/// <param name="Id">Which invitation.</param>
/// <param name="Roles">The roles it names.</param>
/// <param name="IssuedAt">When it was issued.</param>
/// <param name="ExpiresAt">When it expires.</param>
/// <param name="Issuer">Who issued it.</param>
/// <param name="Email">The email it binds, or nothing.</param>
/// <param name="Phone">The phone it binds, or nothing.</param>
/// <param name="CorporateAddress">The corporate address it binds, or nothing.</param>
/// <remarks>Implements IDN-READ-002 (D-218).</remarks>
public sealed record StandingInvitation(
    InvitationId Id,
    IReadOnlyList<RoleName> Roles,
    DateTimeOffset IssuedAt,
    DateTimeOffset ExpiresAt,
    SubjectId Issuer,
    string? Email,
    string? Phone,
    string? CorporateAddress);
