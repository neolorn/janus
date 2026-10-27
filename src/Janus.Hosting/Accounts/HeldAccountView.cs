using System;
using System.Collections.Generic;
using System.Linq;
using Janus.Core;

namespace Janus.Hosting.Accounts;

/// <summary>
/// Another person's account, as <c>GET /admin/accounts/{subject}</c> answers it.
/// </summary>
/// <param name="Subject">Whose account.</param>
/// <param name="State">Where the account stands.</param>
/// <param name="CreatedAt">When the account was created.</param>
/// <param name="Profile">The names the person gave.</param>
/// <param name="Identifiers">What the account is known by.</param>
/// <param name="Preferences">The language, the time zone and the declared values.</param>
/// <param name="Memberships">The organizations the account is a current member of.</param>
/// <remarks>Implements IDN-READ-003 and chapter 09 section 8a.</remarks>
internal sealed record HeldAccountView(
    Guid Subject,
    AccountState State,
    DateTimeOffset CreatedAt,
    HeldProfileView Profile,
    HeldIdentifiersView Identifiers,
    PreferencesView Preferences,
    IReadOnlyList<Guid> Memberships)
{
    /// <summary>
    /// Reads the account.
    /// </summary>
    /// <param name="account">The account.</param>
    /// <returns>The view.</returns>
    /// <exception cref="ArgumentNullException">The account is absent.</exception>
    public static HeldAccountView Of(Core.AccountView account)
    {
        ArgumentNullException.ThrowIfNull(account);

        return new HeldAccountView(
            account.Subject.Value,
            account.State,
            account.CreatedAt,
            new HeldProfileView(account.Profile.DisplayName, account.Profile.LegalName),
            new HeldIdentifiersView(
                [.. account.Identifiers.Emails.Select(HeldIdentifierView.Of)],
                [.. account.Identifiers.Phones.Select(HeldIdentifierView.Of)],
                account.Identifiers.Username),
            PreferencesView.Of(account.Preferences),
            [.. account.Memberships.Select(organization => organization.Value)]);
    }
}
