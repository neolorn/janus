using System.Collections.Generic;

namespace Janus.Core;

/// <summary>
/// An account as the person who holds it sees it: the groups of REG-ACCT-001, every
/// field they may see and nothing they may not.
/// </summary>
/// <param name="State">Which of the account states it stands in.</param>
/// <param name="Identifiers">What it is reached at.</param>
/// <param name="Credentials">What signs in to it.</param>
/// <param name="RecoveryCodes">
/// What its recovery codes stand at, absent where it holds none.
/// </param>
/// <param name="Profile">What it says about the person.</param>
/// <param name="Preferences">What it has settled about how it is addressed.</param>
/// <remarks>Implements REG-ACCT-001 and LIB-API-005.</remarks>
public sealed record AccountDetail(
    AccountState State,
    AccountIdentifiers Identifiers,
    IReadOnlyList<CredentialSummary> Credentials,
    RecoveryCodeStatus? RecoveryCodes,
    ProfileDetail Profile,
    PreferenceValues Preferences);
