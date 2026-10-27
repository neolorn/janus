using System;

namespace Janus.Hosting.Accounts;

/// <summary>
/// The body of <c>POST /admin/accounts/find</c>, which carries the identifier so that no
/// address a proxy logs names it.
/// </summary>
/// <param name="Identifier">The email address, phone number or username.</param>
/// <param name="Organization">The organization the finding is asked in, or null.</param>
/// <remarks>Implements IDN-READ-003 and chapter 09 section 8a.</remarks>
internal sealed record FindAccountRequest(string? Identifier, Guid? Organization);
