namespace Janus.Core;

/// <summary>
/// What an edit of the profile carries. A field left absent is left alone; a field
/// present and empty is cleared.
/// </summary>
/// <param name="DisplayName">The display name, where the edit sets one.</param>
/// <param name="LegalName">The legal name, where the edit sets one.</param>
/// <param name="Username">The username, where the edit sets one.</param>
/// <param name="DateOfBirth">
/// The date of birth, which the person never sets: an edit that carries it is refused,
/// because the date is corrected through support and nowhere else.
/// </param>
/// <remarks>Implements REG-PROF-001 and REG-IDENT-009.</remarks>
public sealed record ProfileEdit(
    string? DisplayName = null,
    string? LegalName = null,
    string? Username = null,
    string? DateOfBirth = null);
