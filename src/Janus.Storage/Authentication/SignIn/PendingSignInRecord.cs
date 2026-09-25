using System;
using Janus.Core;

namespace Janus.Storage.Authentication.SignIn;

/// <summary>
/// The <c>signin_links</c> row: one sign-in link or code that has gone out and not
/// yet been used.
/// </summary>
/// <remarks>
/// Implements AUTH-FACT-003 and REG-SESS-003. The code is held as the message sent
/// it, because the browser that opened the link elsewhere is shown it to type where
/// the sign-in began; the requesting browser is a fingerprint, so the row proves
/// which browser asked without naming it.
/// </remarks>
internal sealed class PendingSignInRecord
{
    /// <summary>The <c>token</c> column: what the link's token hashes to.</summary>
    public byte[] Token { get; set; } = [];

    /// <summary>The <c>subject</c> column.</summary>
    public SubjectId Subject { get; set; }

    /// <summary>The <c>factor</c> column: which catalogue entry it stands for.</summary>
    public Factor Factor { get; set; }

    /// <summary>The <c>email</c> column: the address it went to, where it went to one.</summary>
    public IdentifierId? Email { get; set; }

    /// <summary>The <c>code</c> column.</summary>
    public byte[] Code { get; set; } = [];

    /// <summary>The <c>browser</c> column: what the asking browser carried.</summary>
    public byte[]? Browser { get; set; }

    /// <summary>The <c>issued_at</c> column.</summary>
    public DateTimeOffset IssuedAt { get; set; }

    /// <summary>The <c>expires_at</c> column, which the sweep reads.</summary>
    public DateTimeOffset ExpiresAt { get; set; }

    /// <summary>The <c>wrong_attempts</c> column.</summary>
    public int WrongAttempts { get; set; }
}
