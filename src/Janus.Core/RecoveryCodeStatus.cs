using System;

namespace Janus.Core;

/// <summary>
/// What the account's recovery codes stand at, without any of the codes themselves.
/// </summary>
/// <param name="Remaining">How many are unspent.</param>
/// <param name="GeneratedAt">When the set was drawn.</param>
/// <param name="ViewedAt">When the person last looked at them.</param>
/// <param name="ExportedAt">When the person last took a copy away.</param>
/// <param name="RemindedAt">
/// When the set's one reminder fired, which the account shows until the set is
/// regenerated; nothing before then.
/// </param>
/// <remarks>Implements REG-ACCT-001, AUTH-RECOV-006 and AUTH-FACT-008.</remarks>
public sealed record RecoveryCodeStatus(
    int Remaining,
    DateTimeOffset GeneratedAt,
    DateTimeOffset? ViewedAt,
    DateTimeOffset? ExportedAt,
    DateTimeOffset? RemindedAt);
