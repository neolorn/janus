using System;

namespace Janus.Core;

/// <summary>
/// An invitation as its issue answers it.
/// </summary>
/// <param name="Id">The invitation.</param>
/// <param name="ExpiresAt">When its link stops opening anything.</param>
/// <param name="Token">
/// The token its link carries, where the invitation binds no email for the link to go
/// to and the administrator hands it over; nothing where it was sent.
/// </param>
/// <remarks>Implements IDN-LIFE-009a, REG-INV-001 and chapter 09 section 8a.</remarks>
public sealed record IssuedInvitation(InvitationId Id, DateTimeOffset ExpiresAt, [property: NeverLogged] string? Token);
