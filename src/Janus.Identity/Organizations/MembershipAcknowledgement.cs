using System;
using System.Collections.Generic;
using Janus.Core;

namespace Janus.Identity.Organizations;

/// <summary>
/// What the person acknowledged at the membership step, as the membership keeps it.
/// </summary>
/// <param name="Documents">The documents shown, each at the version shown.</param>
/// <param name="At">When the person acknowledged them.</param>
/// <remarks>Implements REG-INV-001 and IDN-LIFE-009a.</remarks>
internal sealed record MembershipAcknowledgement(IReadOnlyList<InvitationDocument> Documents, DateTimeOffset At);
