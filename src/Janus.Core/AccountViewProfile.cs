namespace Janus.Core;

/// <summary>
/// The names on another person's account.
/// </summary>
/// <param name="DisplayName">The name the person is shown by.</param>
/// <param name="LegalName">
/// The legal name, nothing while <c>profile.legalname</c> is off.
/// </param>
/// <remarks>Implements IDN-READ-003 and REG-PROF-001.</remarks>
public sealed record AccountViewProfile(string? DisplayName, string? LegalName);
