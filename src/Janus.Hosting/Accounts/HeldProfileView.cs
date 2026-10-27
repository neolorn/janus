namespace Janus.Hosting.Accounts;

/// <summary>
/// The names on another person's account.
/// </summary>
/// <param name="DisplayName">The name the person is shown by.</param>
/// <param name="LegalName">The legal name, null while <c>profile.legalname</c> is off.</param>
/// <remarks>Implements IDN-READ-003.</remarks>
internal sealed record HeldProfileView(string? DisplayName, string? LegalName);
