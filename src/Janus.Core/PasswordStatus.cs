using System;

namespace Janus.Core;

/// <summary>
/// Whether the account holds a password, and when it was last set, without anything of
/// the password itself.
/// </summary>
/// <param name="Set">Whether a password is set.</param>
/// <param name="ChangedAt">When it was last set; null where none is.</param>
/// <remarks>
/// Implements REG-ACCT-001 and FE-SEC-001 (D-224): the screens that hold the second-step
/// controls disabled until a password exists read it here, and the credential list
/// stays the authenticators'.
/// </remarks>
public sealed record PasswordStatus(bool Set, DateTimeOffset? ChangedAt);
