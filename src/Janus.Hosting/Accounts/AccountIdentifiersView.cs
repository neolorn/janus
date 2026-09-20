using System;
using System.Collections.Generic;
using System.Linq;
using Janus.Core;

namespace Janus.Hosting.Accounts;

/// <summary>
/// What an account is known by.
/// </summary>
/// <param name="Emails">The email addresses.</param>
/// <param name="Phones">The phone numbers.</param>
/// <param name="Username">The username, absent while usernames are off.</param>
/// <param name="Backup">Which of them a security notice reaches.</param>
/// <remarks>Implements REG-ACCT-001 and REG-IDENT-001.</remarks>
internal sealed record AccountIdentifiersView(
    IReadOnlyList<IdentifierView> Emails,
    IReadOnlyList<IdentifierView> Phones,
    string? Username,
    BackupView Backup)
{
    /// <summary>
    /// Reads what an account is known by.
    /// </summary>
    /// <param name="identifiers">The identifiers.</param>
    /// <returns>The view.</returns>
    /// <exception cref="ArgumentNullException">The identifiers are absent.</exception>
    public static AccountIdentifiersView Of(AccountIdentifiers identifiers)
    {
        ArgumentNullException.ThrowIfNull(identifiers);

        return new AccountIdentifiersView(
            [.. identifiers.Emails.Select(IdentifierView.Of)],
            [.. identifiers.Phones.Select(IdentifierView.Of)],
            identifiers.Username,
            BackupView.Of(identifiers.Backup));
    }
}
