namespace Janus.Hosting.Accounts;

/// <summary>
/// The profile fields the person may edit, and the username where it is enabled.
/// </summary>
/// <param name="DisplayName">What the person is called.</param>
/// <param name="LegalName">The legal name, where the host collects one.</param>
/// <param name="Username">The username, where usernames are enabled.</param>
/// <param name="DateOfBirth">
/// Present only so that sending it can be refused: the date of birth is corrected
/// through support and never by the person (REG-PROF-001).
/// </param>
/// <remarks>Implements REG-PROF-001 and REG-IDENT-009.</remarks>
internal sealed record ProfileEditRequest(
    string? DisplayName,
    string? LegalName,
    string? Username,
    string? DateOfBirth);
