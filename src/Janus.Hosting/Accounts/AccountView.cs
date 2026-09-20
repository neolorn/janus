using System;
using System.Collections.Generic;
using System.Linq;
using Janus.Core;

namespace Janus.Hosting.Accounts;

/// <summary>
/// The account, in the groups REG-ACCT-001 names: every field the person may see and
/// nothing else.
/// </summary>
/// <param name="State">Where the account stands.</param>
/// <param name="Identifiers">What it is known by.</param>
/// <param name="Credentials">What it signs in with.</param>
/// <param name="SecondStep">Which second step is offered first.</param>
/// <param name="RecoveryCodes">How the recovery codes stand.</param>
/// <param name="Profile">The profile fields the host collects.</param>
/// <param name="Preferences">The language, the time zone and the declared values.</param>
/// <remarks>Implements REG-ACCT-001.</remarks>
internal sealed record AccountView(
    AccountState State,
    AccountIdentifiersView Identifiers,
    IReadOnlyList<CredentialView> Credentials,
    SecondStepView SecondStep,
    RecoveryCodeView? RecoveryCodes,
    ProfileView Profile,
    PreferencesView Preferences)
{
    /// <summary>
    /// Reads the account.
    /// </summary>
    /// <param name="account">The account.</param>
    /// <returns>The view.</returns>
    /// <exception cref="ArgumentNullException">The account is absent.</exception>
    public static AccountView Of(AccountDetail account)
    {
        ArgumentNullException.ThrowIfNull(account);

        return new AccountView(
            account.State,
            AccountIdentifiersView.Of(account.Identifiers),
            [.. account.Credentials.Select(CredentialView.Of)],
            new SecondStepView(Preferred(account.Credentials)),
            RecoveryCodeView.Of(account.RecoveryCodes),
            ProfileView.Of(account.Profile),
            PreferencesView.Of(account.Preferences));
    }

    // IDN-ATTR-008: the order is derived, so the one offered first is read off the
    // list rather than held anywhere of its own.
    private static string? Preferred(IReadOnlyList<CredentialSummary> credentials)
    {
        foreach (CredentialSummary credential in credentials)
        {
            if (credential.Preferred)
            {
                return credential.Id.ToString();
            }
        }

        return null;
    }
}
