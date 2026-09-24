using System;

namespace Janus.Hosting.Accounts;

/// <summary>
/// Which invitation the membership step showed and the person acknowledges.
/// </summary>
/// <param name="InvitationId">The invitation.</param>
/// <remarks>Implements REG-INV-001 and REG-INV-002.</remarks>
internal sealed record AcknowledgeInvitationRequest(Guid? InvitationId);
