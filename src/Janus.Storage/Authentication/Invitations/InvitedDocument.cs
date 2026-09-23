namespace Janus.Storage.Authentication.Invitations;

/// <summary>
/// One document an invitation shows, as its column holds it.
/// </summary>
/// <param name="Document">Which document.</param>
/// <param name="Version">The version current when the invitation was issued.</param>
/// <remarks>Implements REG-INV-001.</remarks>
internal sealed record InvitedDocument(string Document, string Version);
