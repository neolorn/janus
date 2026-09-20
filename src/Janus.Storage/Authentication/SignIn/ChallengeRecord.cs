using System;
using Janus.Core;

namespace Janus.Storage.Authentication.SignIn;

/// <summary>
/// The <c>signin_challenges</c> row: one sign-in in progress.
/// </summary>
/// <remarks>
/// Implements AUTH-ABUSE-003, AUTH-FACT-014 and AUTH-FACT-016. Nothing here is a
/// personal field, so no key is unwrapped and none is needed: the row is a
/// fingerprint, a subject, the value a ceremony signs over and what has been
/// presented. The subject is absent where the identifier resolved to no account,
/// because a challenge exists either way.
/// </remarks>
internal sealed class ChallengeRecord
{
    /// <summary>The <c>handle</c> column: what the caller's handle hashes to.</summary>
    public byte[] Handle { get; set; } = [];

    /// <summary>The <c>subject</c> column, absent where the identifier resolved to none.</summary>
    public SubjectId? Subject { get; set; }

    /// <summary>The <c>webauthn</c> column: what an assertion has to sign over.</summary>
    public string WebAuthn { get; set; } = string.Empty;

    /// <summary>The <c>created_at</c> column.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>The <c>expires_at</c> column, which the sweep reads.</summary>
    public DateTimeOffset ExpiresAt { get; set; }

    /// <summary>The <c>presented</c> column: the catalogue entries accepted so far.</summary>
    public string[] Presented { get; set; } = [];

    /// <summary>The <c>device_code</c> column: the new-device code outstanding.</summary>
    public byte[]? DeviceCode { get; set; }

    /// <summary>The <c>device_attempts</c> column.</summary>
    public int DeviceAttempts { get; set; }
}
