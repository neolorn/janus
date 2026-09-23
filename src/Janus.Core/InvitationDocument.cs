namespace Janus.Core;

/// <summary>
/// One document an organization attached to an invitation, at the version the person
/// is shown and acknowledges.
/// </summary>
/// <param name="Document">Which document.</param>
/// <param name="Version">Which version of it.</param>
/// <remarks>Implements REG-INV-001 and IDN-LIFE-009a.</remarks>
public sealed record InvitationDocument(string Document, string Version);
