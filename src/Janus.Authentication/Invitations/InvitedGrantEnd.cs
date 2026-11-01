using System;
using System.Collections.Generic;
using Janus.Core;

namespace Janus.Authentication.Invitations;

/// <summary>
/// The end an invitation gives the grants of its roles whose grants must end.
/// </summary>
/// <param name="At">When those grants stop, later than the invitation's own expiry.</param>
/// <param name="Roles">The roles it applies to: those of the invitation whose grants had to end at issue.</param>
/// <remarks>
/// Implements AUTHZ-GRANT-003 (D-237, D-239). The roles are kept as they stood at issue,
/// so the membership attaches as the invitation was issued whatever becomes of a role's
/// rule since.
/// </remarks>
internal sealed record InvitedGrantEnd(DateTimeOffset At, IReadOnlyList<RoleName> Roles);
