using System;

namespace Janus.Core;

/// <summary>
/// What the account's recovery codes stand at, without any of the codes themselves.
/// </summary>
/// <param name="Remaining">How many are unspent.</param>
/// <param name="GeneratedAt">When the set was drawn.</param>
/// <param name="ViewedAt">When the person last looked at them.</param>
/// <param name="ExportedAt">When the person last took a copy away.</param>
/// <remarks>Implements REG-ACCT-001 and AUTH-RECOV-006.</remarks>
public sealed record RecoveryCodeStatus(
    int Remaining,
    DateTimeOffset GeneratedAt,
    DateTimeOffset? ViewedAt,
    DateTimeOffset? ExportedAt);
