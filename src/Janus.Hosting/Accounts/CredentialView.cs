using System;
using Janus.Core;

namespace Janus.Hosting.Accounts;

/// <summary>
/// One enrolled authenticator, by property and label and never by secret material.
/// </summary>
/// <param name="Id">What the credential endpoints name it by.</param>
/// <param name="Kind">Which factor it is.</param>
/// <param name="Label">What the person called it.</param>
/// <param name="State">Where it stands.</param>
/// <param name="InvalidatesAt">When a reported-lost one stops working.</param>
/// <param name="BackupEligible">Whether the authenticator can be synced.</param>
/// <param name="BackupState">Whether it is synced.</param>
/// <param name="AddedAt">When it was enrolled.</param>
/// <param name="LastUsedAt">When it was last presented.</param>
/// <remarks>Implements REG-ACCT-001, AUTH-FACT-001 and AUTH-FACT-009.</remarks>
internal sealed record CredentialView(
    string Id,
    Factor Kind,
    string Label,
    AuthenticatorState State,
    DateTimeOffset? InvalidatesAt,
    bool? BackupEligible,
    bool? BackupState,
    DateTimeOffset AddedAt,
    DateTimeOffset? LastUsedAt)
{
    /// <summary>
    /// Reads one credential.
    /// </summary>
    /// <param name="credential">The credential.</param>
    /// <returns>The view.</returns>
    /// <exception cref="ArgumentNullException">The credential is absent.</exception>
    public static CredentialView Of(CredentialSummary credential)
    {
        ArgumentNullException.ThrowIfNull(credential);

        return new CredentialView(
            credential.Id.ToString(),
            credential.Kind,
            credential.Label,
            credential.State,
            credential.InvalidatesAt,
            credential.BackupEligible,
            credential.BackupState,
            credential.AddedAt,
            credential.LastUsedAt);
    }
}
