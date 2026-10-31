using System;
using Janus.Core;

namespace Janus.Authentication.Oidc;

/// <summary>
/// An authorization request that found no live session and was not silent, kept for
/// the sign-in that answers it.
/// </summary>
/// <remarks>
/// Implements AUTH-SESS-012 (D-224, D-226). It holds the request's validated
/// parameters, so its pushed request is spent as before and no destination crosses in
/// the browser's hands; it is bound first to the browser's pre-authentication session
/// and then to the session record the sign-in establishes, and it lives for
/// <c>oidc.continuation.lifetime</c> from its creation wherever it is bound.
/// </remarks>
internal sealed class PendingAuthorization
{
    private PendingAuthorization(
        byte[] preAuthentication,
        string parameters,
        DateTimeOffset createdAt,
        DateTimeOffset expiresAt)
    {
        PreAuthentication = preAuthentication;
        Parameters = parameters;
        CreatedAt = createdAt;
        ExpiresAt = expiresAt;
    }

    /// <summary>
    /// What the cookie of the pre-authentication session it was kept for fingerprints
    /// to.
    /// </summary>
    [NeverLogged]
    public byte[] PreAuthentication { get; }

    /// <summary>
    /// The request's validated parameters, as the provider reads them back.
    /// </summary>
    [NeverLogged]
    public string Parameters { get; }

    /// <summary>
    /// When the request was kept.
    /// </summary>
    public DateTimeOffset CreatedAt { get; }

    /// <summary>
    /// When it stops answering, which is <c>oidc.continuation.lifetime</c> after it was
    /// kept.
    /// </summary>
    public DateTimeOffset ExpiresAt { get; }

    /// <summary>
    /// Keeps a request for a browser.
    /// </summary>
    /// <param name="preAuthentication">What the browser's cookie fingerprints to.</param>
    /// <param name="parameters">The request's validated parameters.</param>
    /// <param name="at">When it is kept.</param>
    /// <param name="lifetime">How long it waits.</param>
    /// <returns>The pending authorization.</returns>
    /// <exception cref="ArgumentNullException">A part is absent.</exception>
    public static PendingAuthorization Keep(
        byte[] preAuthentication,
        string parameters,
        DateTimeOffset at,
        TimeSpan lifetime)
    {
        ArgumentNullException.ThrowIfNull(preAuthentication);
        ArgumentException.ThrowIfNullOrEmpty(parameters);

        return new PendingAuthorization(preAuthentication, parameters, at, at + lifetime);
    }

    /// <summary>
    /// Whether it has stopped answering.
    /// </summary>
    /// <param name="now">The instant to judge at.</param>
    /// <returns>Whether it has.</returns>
    public bool HasExpired(DateTimeOffset now) => now >= ExpiresAt;
}
