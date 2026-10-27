using System.Collections.Generic;

namespace Janus.Hosting.Accounts;

/// <summary>
/// What another person's account is known by.
/// </summary>
/// <param name="Emails">Its email addresses.</param>
/// <param name="Phones">Its phone numbers.</param>
/// <param name="Username">Its username, null where it holds none or usernames are off.</param>
/// <remarks>Implements IDN-READ-003.</remarks>
internal sealed record HeldIdentifiersView(
    IReadOnlyList<HeldIdentifierView> Emails,
    IReadOnlyList<HeldIdentifierView> Phones,
    string? Username);
