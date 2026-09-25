using Janus.Core;

namespace Janus.Hosting.Registration;

/// <summary>
/// What starts a registration.
/// </summary>
/// <param name="ClientId">
/// The application the person came from, recorded on the session so that the end of
/// registration can return them to it (API-REDIR-002).
/// </param>
/// <param name="InvitationToken">
/// The token of the invitation link the person pressed, where the registration is
/// one an invitation opens (REG-INV-001).
/// </param>
/// <remarks>Implements REG-SESS-001, REG-INV-001 and API-REDIR-002.</remarks>
internal sealed record BeginRegistrationRequest(string? ClientId, [property: NeverLogged] string? InvitationToken);
