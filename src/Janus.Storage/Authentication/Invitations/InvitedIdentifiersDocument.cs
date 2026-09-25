namespace Janus.Storage.Authentication.Invitations;

/// <summary>
/// What an invitation binds, as its encrypted column holds it.
/// </summary>
/// <param name="Email">The bound email as the administrator entered it.</param>
/// <param name="Phone">The bound phone as the administrator entered it.</param>
/// <param name="CorporateEmail">The asserted corporate address as the administrator entered it.</param>
/// <remarks>Implements REG-INV-001 and REG-MAIL-001.</remarks>
internal sealed record InvitedIdentifiersDocument(string? Email, string? Phone, string? CorporateEmail);
