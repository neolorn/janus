using System;

namespace Janus.Core;

/// <summary>
/// One credential enrolled on an account, reported by property and label and never by
/// secret material.
/// </summary>
/// <param name="Id">Which credential, as the credential endpoints name it.</param>
/// <param name="Kind">The catalogue entry it is.</param>
/// <param name="Label">The name the person gave it.</param>
/// <param name="State">Whether it is active, suspended or invalidated.</param>
/// <param name="InvalidatesAt">
/// When a suspended credential is invalidated, absent where none is pending.
/// </param>
/// <param name="BackupEligible">
/// Whether the authenticator may synchronise the credential, where it says.
/// </param>
/// <param name="BackupState">
/// Whether it is synchronised at present, where the authenticator says.
/// </param>
/// <param name="Preferred">Whether it is the account's preferred second step.</param>
/// <param name="AddedAt">When it was enrolled.</param>
/// <param name="LastUsedAt">When it was last presented.</param>
/// <remarks>
/// Implements REG-ACCT-001, AUTH-FACT-001, AUTH-FACT-008, AUTH-FACT-009, AUTH-RECOV-007
/// and IDN-ATTR-008.
/// </remarks>
public sealed record CredentialSummary(
    AuthenticatorId Id,
    Factor Kind,
    string Label,
    AuthenticatorState State,
    DateTimeOffset? InvalidatesAt,
    bool? BackupEligible,
    bool? BackupState,
    bool Preferred,
    DateTimeOffset AddedAt,
    DateTimeOffset? LastUsedAt);
