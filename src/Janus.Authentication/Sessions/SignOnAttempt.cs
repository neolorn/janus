namespace Janus.Authentication.Sessions;

/// <summary>
/// The sign-on a browser has in flight: what it must return, what redeems the code it
/// returns with, and where it was going.
/// </summary>
/// <param name="StateFingerprint">
/// What the <c>state</c> value handed to the browser fingerprints to, so the row holds
/// nothing the browser holds.
/// </param>
/// <param name="Verifier">
/// The proof key the token request presents, which never leaves this server and is at
/// rest under the key-encryption key.
/// </param>
/// <param name="ReturnTo">
/// The path on this application the browser was going to, which it is sent back to
/// once the session exists.
/// </param>
/// <remarks>
/// Implements BFF-SESS-006 and BFF-CSRF-005a. A browser has at most one sign-on in
/// flight, bound to the pre-authentication session it started from, so a code that
/// comes back to a browser that started nothing is refused before anything is
/// exchanged.
/// </remarks>
internal sealed record SignOnAttempt(byte[] StateFingerprint, string Verifier, string ReturnTo);
