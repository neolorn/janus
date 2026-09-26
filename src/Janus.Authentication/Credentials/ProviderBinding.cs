using Janus.Core;

namespace Janus.Authentication.Credentials;

/// <summary>
/// The browser a round trip to a social provider belongs to: its pre-authentication
/// session before it is signed in, or its session once it is.
/// </summary>
/// <param name="PreAuthentication">
/// What the pre-authentication cookie fingerprints to, where the browser holds no
/// session.
/// </param>
/// <param name="Session">The session, where the browser is signed in.</param>
/// <remarks>Implements BFF-CSRF-005a and IDN-LIFE-012. Exactly one of the two is set.</remarks>
internal sealed record ProviderBinding(byte[]? PreAuthentication, SessionId? Session)
{
    /// <summary>
    /// The binding of a browser that is not signed in.
    /// </summary>
    /// <param name="preAuthentication">What its pre-authentication cookie fingerprints to.</param>
    /// <returns>The binding.</returns>
    public static ProviderBinding Before(byte[] preAuthentication) => new(preAuthentication, null);

    /// <summary>
    /// The binding of a browser that is signed in.
    /// </summary>
    /// <param name="session">Its session.</param>
    /// <returns>The binding.</returns>
    public static ProviderBinding Within(SessionId session) => new(null, session);
}
