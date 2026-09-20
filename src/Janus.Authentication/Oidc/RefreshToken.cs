using System;
using Janus.Core;

namespace Janus.Authentication.Oidc;

/// <summary>
/// One refresh token as the store holds it: the session it is a handle on, the family
/// every rotation of it belongs to, and whether it has been used.
/// </summary>
/// <remarks>
/// Implements AUTH-OIDC-003. The token is not a credential of its own: it cannot
/// outlive the session record it derives from, and presenting a token that has already
/// been used revokes the whole family, because either the client or a thief is holding
/// a copy and there is no telling which.
/// </remarks>
internal sealed class RefreshToken
{
    private RefreshToken(
        byte[] fingerprint,
        RefreshFamilyId family,
        string clientId,
        SubjectId subject,
        SessionId session,
        string scope,
        DateTimeOffset issuedAt,
        DateTimeOffset expiresAt)
    {
        Fingerprint = fingerprint;
        Family = family;
        ClientId = clientId;
        Subject = subject;
        Session = session;
        Scope = scope;
        IssuedAt = issuedAt;
        ExpiresAt = expiresAt;
    }

    /// <summary>What the token the client holds hashes to.</summary>
    public byte[] Fingerprint { get; }

    /// <summary>Which family it belongs to, which is what a reuse revokes.</summary>
    public RefreshFamilyId Family { get; }

    /// <summary>Which client holds it, and no other may present it.</summary>
    public string ClientId { get; }

    /// <summary>Whose account it is for.</summary>
    public SubjectId Subject { get; }

    /// <summary>The session record it is a handle on.</summary>
    public SessionId Session { get; }

    /// <summary>What it covers.</summary>
    public string Scope { get; }

    /// <summary>When it was issued.</summary>
    public DateTimeOffset IssuedAt { get; }

    /// <summary>
    /// When it stops working, which is never later than the session record's own
    /// absolute expiry.
    /// </summary>
    public DateTimeOffset ExpiresAt { get; }

    /// <summary>When it was used, and nothing while it is unused.</summary>
    public DateTimeOffset? ConsumedAt { get; private set; }

    /// <summary>
    /// Issues one.
    /// </summary>
    /// <param name="fingerprint">What the token hashes to.</param>
    /// <param name="family">The family it belongs to.</param>
    /// <param name="clientId">Which client holds it.</param>
    /// <param name="subject">Whose account.</param>
    /// <param name="session">The session record it is a handle on.</param>
    /// <param name="scope">What it covers.</param>
    /// <param name="at">Now.</param>
    /// <param name="expiresAt">When the session record itself stops.</param>
    /// <returns>The token.</returns>
    /// <exception cref="ArgumentNullException">The fingerprint is absent.</exception>
    public static RefreshToken Issue(
        byte[] fingerprint,
        RefreshFamilyId family,
        string clientId,
        SubjectId subject,
        SessionId session,
        string scope,
        DateTimeOffset at,
        DateTimeOffset expiresAt)
    {
        ArgumentNullException.ThrowIfNull(fingerprint);

        return new RefreshToken(fingerprint, family, clientId, subject, session, scope, at, expiresAt);
    }

    /// <summary>
    /// The token as the store holds it.
    /// </summary>
    /// <param name="fingerprint">What the token hashes to.</param>
    /// <param name="family">The family it belongs to.</param>
    /// <param name="clientId">Which client holds it.</param>
    /// <param name="subject">Whose account.</param>
    /// <param name="session">The session record it is a handle on.</param>
    /// <param name="scope">What it covers.</param>
    /// <param name="issuedAt">When it was issued.</param>
    /// <param name="expiresAt">When it stops working.</param>
    /// <param name="consumedAt">When it was used, or nothing.</param>
    /// <returns>The token.</returns>
    /// <exception cref="ArgumentNullException">The fingerprint is absent.</exception>
    public static RefreshToken Existing(
        byte[] fingerprint,
        RefreshFamilyId family,
        string clientId,
        SubjectId subject,
        SessionId session,
        string scope,
        DateTimeOffset issuedAt,
        DateTimeOffset expiresAt,
        DateTimeOffset? consumedAt)
    {
        ArgumentNullException.ThrowIfNull(fingerprint);

        return new RefreshToken(fingerprint, family, clientId, subject, session, scope, issuedAt, expiresAt)
        {
            ConsumedAt = consumedAt,
        };
    }

    /// <summary>
    /// Whether it may be rotated: unused, unexpired, and held by the client presenting
    /// it.
    /// </summary>
    /// <param name="clientId">Which client is presenting it.</param>
    /// <param name="now">Now.</param>
    /// <returns>Whether it still refreshes anything.</returns>
    public bool Rotates(string clientId, DateTimeOffset now) =>
        ConsumedAt is null && now < ExpiresAt && string.Equals(ClientId, clientId, StringComparison.Ordinal);

    /// <summary>
    /// The token was used, which is the last thing it does.
    /// </summary>
    /// <param name="at">When.</param>
    public void Consume(DateTimeOffset at) => ConsumedAt = at;
}
