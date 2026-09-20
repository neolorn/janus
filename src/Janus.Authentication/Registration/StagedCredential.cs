using Janus.Authentication.Factors;
using Janus.Core;

namespace Janus.Authentication.Registration;

/// <summary>
/// One credential enrolled against a registration session, held until the account
/// exists to hold it.
/// </summary>
/// <param name="Id">The identifier the credential will carry.</param>
/// <param name="Factor">Which catalogue entry it is an instance of.</param>
/// <param name="Label">What the person calls it.</param>
/// <param name="Totp">The shared secret, where it is a code generator.</param>
/// <param name="WebAuthn">The key material, where it holds a key.</param>
/// <remarks>
/// Implements REG-SESS-001 and REG-SESS-006. A ceremony run against the session uses
/// the session's provisional user handle, which becomes the subject identifier, so
/// nothing about the credential changes when the account comes into being.
/// </remarks>
internal sealed record StagedCredential(
    AuthenticatorId Id,
    Factor Factor,
    CredentialLabel Label,
    TotpMaterial? Totp,
    WebAuthnMaterial? WebAuthn);
