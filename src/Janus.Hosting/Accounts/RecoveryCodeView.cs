using System;
using Janus.Core;

namespace Janus.Hosting.Accounts;

/// <summary>
/// How the account's recovery codes stand.
/// </summary>
/// <param name="Remaining">How many are unused.</param>
/// <param name="GeneratedAt">When the set was drawn.</param>
/// <param name="ViewedAt">When the person last looked at them.</param>
/// <param name="ExportedAt">When the person last took a copy away.</param>
/// <param name="RemindedAt">
/// When the set's one reminder fired, which the account shows until the set is
/// regenerated.
/// </param>
/// <remarks>Implements REG-ACCT-001 and AUTH-FACT-008.</remarks>
internal sealed record RecoveryCodeView(
    int Remaining,
    DateTimeOffset GeneratedAt,
    DateTimeOffset? ViewedAt,
    DateTimeOffset? ExportedAt,
    DateTimeOffset? RemindedAt)
{
    /// <summary>
    /// Reads the set.
    /// </summary>
    /// <param name="status">Where the set stands.</param>
    /// <returns>The view, or nothing where the account has no set.</returns>
    public static RecoveryCodeView? Of(RecoveryCodeStatus? status) =>
        status is null
            ? null
            : new RecoveryCodeView(
                status.Remaining,
                status.GeneratedAt,
                status.ViewedAt,
                status.ExportedAt,
                status.RemindedAt);
}
