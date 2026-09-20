namespace Janus.Core;

/// <summary>
/// What the userinfo endpoint tells a client about the person, by the scopes the
/// token covers and nothing beyond them.
/// </summary>
/// <param name="Subject">Whose account it is, which every scope carries.</param>
/// <param name="Email">The primary address, where the token covers <c>email</c>.</param>
/// <param name="EmailVerified">Whether that address is verified.</param>
/// <param name="Name">The display name, where the token covers <c>profile</c>.</param>
/// <param name="PreferredUsername">
/// The username, where the token covers <c>profile</c> and the deployment holds
/// usernames.
/// </param>
/// <param name="Locale">The language preference, where the token covers <c>profile</c>.</param>
/// <remarks>
/// Implements AUTH-OIDC-001 and chapter 09 section 9 (D-153). Nothing else is issued:
/// no phone number, legal name, date of birth or photo.
/// </remarks>
public sealed record OidcClaims(
    SubjectId Subject,
    string? Email,
    bool? EmailVerified,
    string? Name,
    string? PreferredUsername,
    string? Locale);
