using System;
using Janus.Core;

namespace Janus.Storage.Authentication.SignIn;

/// <summary>
/// The <c>signin_challenges</c> row: one sign-in in progress.
/// </summary>
/// <remarks>
/// Implements AUTH-ABUSE-001, AUTH-ABUSE-003, AUTH-FACT-014, AUTH-FACT-016,
/// REG-DOM-001 and OPS-SEC-003. Nothing here is a personal field, so no key is
/// unwrapped and none is needed: the row is a fingerprint, a subject, the identifier of
/// the email it was opened with, the keyed hash of the identifier as it was entered and
/// the version of the fingerprint key that hash was computed under, the value a
/// ceremony signs over and what has been presented. The subject is absent where the
/// identifier resolved to no account, because a challenge exists either way. The hash
/// and its version are absent together, and only on a row written before challenges
/// carried them (OPS-MIG-005).
/// </remarks>
internal sealed class ChallengeRecord
{
    /// <summary>The <c>handle</c> column: what the caller's handle hashes to.</summary>
    public byte[] Handle { get; set; } = [];

    /// <summary>The <c>subject</c> column, absent where the identifier resolved to none.</summary>
    public SubjectId? Subject { get; set; }

    /// <summary>
    /// The <c>email</c> column: the address the sign-in was opened with, where the
    /// account holds it.
    /// </summary>
    public IdentifierId? Email { get; set; }

    /// <summary>
    /// The <c>identifier</c> column: the keyed hash of the identifier the sign-in was
    /// opened with, which a refused factor is counted against.
    /// </summary>
    public byte[]? Identifier { get; set; }

    /// <summary>
    /// The <c>fingerprint_version</c> column: the version of the fingerprint key the
    /// identifier's hash was computed under.
    /// </summary>
    public int? FingerprintVersion { get; set; }

    /// <summary>The <c>webauthn</c> column: what an assertion has to sign over.</summary>
    public string WebAuthn { get; set; } = string.Empty;

    /// <summary>The <c>created_at</c> column.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>The <c>expires_at</c> column, which the sweep reads.</summary>
    public DateTimeOffset ExpiresAt { get; set; }

    /// <summary>The <c>presented</c> column: the catalogue entries accepted so far.</summary>
    public string[] Presented { get; set; } = [];
}
