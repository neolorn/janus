namespace Janus.Core;

/// <summary>
/// The names on another person's account.
/// </summary>
/// <param name="DisplayName">The name the person is shown by.</param>
/// <param name="LegalName">
/// The legal name, nothing while <c>profile.legalname</c> is off.
/// </param>
/// <param name="GrammaticalGender">
/// How the person is addressed, nothing where the policy in force for the account does
/// not enable it or the person has given none (D-236).
/// </param>
/// <remarks>Implements IDN-READ-003, IDN-ATTR-007 and REG-PROF-001.</remarks>
public sealed record AccountViewProfile(
    string? DisplayName,
    string? LegalName,
    GrammaticalGender? GrammaticalGender);
