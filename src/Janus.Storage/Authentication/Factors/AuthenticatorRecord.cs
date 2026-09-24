using System;
using Janus.Core;

namespace Janus.Storage.Authentication.Factors;

/// <summary>
/// The <c>authenticators</c> row.
/// </summary>
/// <remarks>
/// Implements AUTH-FACT-001, AUTH-FACT-006, AUTH-FACT-013 and CONV-DESIGN-003. One
/// table holds every kind of credential: the columns a kind does not use are absent
/// on its rows, and a new kind is a row and not a table.
/// </remarks>
internal sealed class AuthenticatorRecord
{
    /// <summary>The <c>id</c> column, which is this table's key.</summary>
    public AuthenticatorId Id { get; set; }

    /// <summary>The <c>subject</c> column.</summary>
    public SubjectId Subject { get; set; }

    /// <summary>The <c>factor</c> column.</summary>
    public Factor Factor { get; set; }

    /// <summary>The <c>label</c> column.</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>The <c>state</c> column.</summary>
    public AuthenticatorState State { get; set; }

    /// <summary>The <c>added_at</c> column.</summary>
    public DateTimeOffset AddedAt { get; set; }

    /// <summary>The <c>last_used_at</c> column.</summary>
    public DateTimeOffset? LastUsedAt { get; set; }

    /// <summary>The <c>invalidates_at</c> column.</summary>
    public DateTimeOffset? InvalidatesAt { get; set; }

    /// <summary>The <c>confirmed</c> column.</summary>
    public bool Confirmed { get; set; }

    /// <summary>
    /// The <c>totp_secret</c> column, holding the shared secret under the person's
    /// key (AUTH-FACT-006).
    /// </summary>
    public byte[]? TotpSecret { get; set; }

    /// <summary>The <c>totp_consumed_step</c> column.</summary>
    public long? TotpConsumedStep { get; set; }

    /// <summary>The <c>credential_id</c> column.</summary>
    public byte[]? CredentialId { get; set; }

    /// <summary>The <c>public_key</c> column, which is public and held in clear.</summary>
    public byte[]? PublicKey { get; set; }

    /// <summary>The <c>algorithm</c> column.</summary>
    public int? Algorithm { get; set; }

    /// <summary>The <c>relying_party</c> column.</summary>
    public string? RelyingParty { get; set; }

    /// <summary>The <c>counter</c> column.</summary>
    public long? Counter { get; set; }

    /// <summary>The <c>backup_eligible</c> column.</summary>
    public bool? BackupEligible { get; set; }

    /// <summary>The <c>backup_state</c> column.</summary>
    public bool? BackupState { get; set; }

    /// <summary>The <c>is_preferred</c> column (IDN-ATTR-008).</summary>
    public bool IsPreferred { get; set; }

    /// <summary>
    /// The <c>provider_subject</c> column: the keyed fingerprint of the subject a
    /// social provider knows a linked identity by, which is what its security events
    /// name (IDN-LIFE-012a, PRIV-RIGHT-005c).
    /// </summary>
    public byte[]? ProviderSubject { get; set; }

    /// <summary>
    /// The <c>fingerprint_version</c> column: the version of the fingerprint key the
    /// provider's subject was fingerprinted under, beside a linked identity only.
    /// </summary>
    public int? FingerprintVersion { get; set; }

    /// <summary>
    /// The <c>enc_provider_subject</c> column: the provider's subject itself, under the
    /// account's key, which its fingerprint is computed from again when the fingerprint
    /// key rotates (OPS-SEC-003).
    /// </summary>
    public byte[]? EncryptedProviderSubject { get; set; }
}
