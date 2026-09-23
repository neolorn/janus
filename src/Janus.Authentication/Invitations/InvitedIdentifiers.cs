namespace Janus.Authentication.Invitations;

/// <summary>
/// The identifiers an invitation binds, as the administrator entered them. They are
/// personal data of someone who holds no account yet, so they are kept only while the
/// invitation is open.
/// </summary>
/// <param name="Email">
/// The email the invitation binds and its link goes to: the personal email where the
/// organization's mail is integrated. Nothing where the email is open.
/// </param>
/// <param name="Phone">The phone it binds, or nothing where the phone is open.</param>
/// <param name="CorporateEmail">
/// The corporate address the administrator asserts, where the organization's mail is
/// integrated.
/// </param>
/// <remarks>Implements REG-INV-001, REG-MAIL-001 and REG-IDENT-010.</remarks>
internal sealed record InvitedIdentifiers(string? Email, string? Phone, string? CorporateEmail);
