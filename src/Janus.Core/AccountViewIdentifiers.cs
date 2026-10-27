using System.Collections.Generic;

namespace Janus.Core;

/// <summary>
/// What another person's account is known by.
/// </summary>
/// <param name="Emails">Its email addresses.</param>
/// <param name="Phones">Its phone numbers.</param>
/// <param name="Username">
/// Its username, nothing where it holds none or <c>identifiers.username.enabled</c> is
/// off.
/// </param>
/// <remarks>Implements IDN-READ-003.</remarks>
public sealed record AccountViewIdentifiers(
    IReadOnlyList<AccountViewIdentifier> Emails,
    IReadOnlyList<AccountViewIdentifier> Phones,
    string? Username);
