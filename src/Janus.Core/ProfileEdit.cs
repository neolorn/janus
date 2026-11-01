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
/// <param name="GrammaticalGender">
/// How the person is addressed, <c>masculine</c> or <c>feminine</c>, where the edit sets
/// it (D-236).
/// </param>
/// <remarks>Implements REG-PROF-001, REG-IDENT-009 and IDN-ATTR-007.</remarks>
public sealed record ProfileEdit(
    string? DisplayName = null,
    string? LegalName = null,
    string? Username = null,
    string? DateOfBirth = null,
    string? GrammaticalGender = null);
