using Janus.Core;

namespace Janus.Hosting.Accounts;

/// <summary>
/// The names on another person's account.
/// </summary>
/// <param name="DisplayName">The name the person is shown by.</param>
/// <param name="LegalName">The legal name, null while <c>profile.legalname</c> is off.</param>
/// <param name="GrammaticalGender">
/// How the person is addressed, null where the policy does not enable it or the person
/// has given none (D-236).
/// </param>
/// <remarks>Implements IDN-READ-003 and IDN-ATTR-007.</remarks>
internal sealed record HeldProfileView(string? DisplayName, string? LegalName, GrammaticalGender? GrammaticalGender);
