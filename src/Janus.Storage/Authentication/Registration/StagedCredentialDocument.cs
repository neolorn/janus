using System;

namespace Janus.Storage.Authentication.Registration;

/// <summary>
/// One credential enrolled against a registration session, as it is written into the
/// session's encrypted column.
/// </summary>
/// <param name="Id">The identifier the credential will carry.</param>
/// <param name="Factor">Which entry of the catalogue it is.</param>
/// <param name="Label">What the person called it.</param>
/// <param name="TotpSecret">The shared secret, where it is a code generator.</param>
/// <param name="CredentialId">The credential identifier, where it is a WebAuthn one.</param>
/// <param name="PublicKey">The public key, where it is a WebAuthn one.</param>
/// <param name="Algorithm">The signature algorithm, where it is a WebAuthn one.</param>
/// <param name="RelyingPartyId">The relying party it was created for.</param>
/// <param name="Counter">The signature counter, where the authenticator keeps one.</param>
/// <param name="BackupEligible">Whether the authenticator may back it up.</param>
/// <param name="BackupState">Whether it is backed up.</param>
/// <param name="ProviderSubject">
/// The provider's own identifier for the person, where it is a social provider's
/// identity.
/// </param>
/// <remarks>Implements REG-SESS-006, REG-IDENT-008, AUTH-FACT-001 and AUTH-FACT-006.</remarks>
internal sealed record StagedCredentialDocument(
    Guid Id,
    string Factor,
    string Label,
    byte[]? TotpSecret,
    byte[]? CredentialId,
    byte[]? PublicKey,
    int? Algorithm,
    string? RelyingPartyId,
    uint? Counter,
    bool BackupEligible,
    bool BackupState,
    string? ProviderSubject);
