using Janus.Core;

namespace Janus.Authentication.Credentials;

/// <summary>
/// A round trip to a social provider a browser has in flight: what it must return,
/// what redeems the code it returns with, and where it was going.
/// </summary>
/// <param name="Provider">Which provider the browser was sent to.</param>
/// <param name="Intent">What the round trip is for.</param>
/// <param name="StateFingerprint">
/// What the <c>state</c> value handed to the browser fingerprints to, so the row holds
/// nothing the browser holds.
/// </param>
/// <param name="NonceFingerprint">
/// What the <c>nonce</c> the identity token must carry fingerprints to.
/// </param>
/// <param name="Verifier">
/// The proof key the token request presents, where the provider takes one, which never
/// leaves this server and is at rest under the key-encryption key.
/// </param>
/// <param name="ReturnTo">
/// The path on this application the browser is sent back to once the return is judged.
/// </param>
/// <remarks>
/// Implements IDN-LIFE-012, REG-IDENT-008, BFF-SESS-001 and BFF-CSRF-005a. A browser has
/// at most one in flight, bound to the pre-authentication session or the session it
/// started from, so a code that comes back to a browser that started nothing is refused
/// before anything is exchanged.
/// </remarks>
internal sealed record ProviderAttempt(
    Factor Provider,
    ProviderIntent Intent,
    byte[] StateFingerprint,
    byte[] NonceFingerprint,
    [property: NeverLogged] string? Verifier,
    string ReturnTo);
