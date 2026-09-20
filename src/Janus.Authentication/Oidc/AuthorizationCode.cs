using System;
using Janus.Core;

namespace Janus.Authentication.Oidc;

/// <summary>
/// A one-time authorization code as the store holds it: what it was issued against,
/// and whether it has been exchanged.
/// </summary>
/// <remarks>
/// Implements AUTH-SESS-012. The row holds what the code hashes to and never the code,
/// so a dump of the table exchanges nothing. Exchanging one leaves the row standing as
/// the record that it was spent, which is what catches a second presentation.
/// </remarks>
internal sealed class AuthorizationCode
{
    private AuthorizationCode(
        byte[] fingerprint,
        string clientId,
        SubjectId subject,
        SessionId session,
        string redirect,
        string challenge,
        string challengeMethod,
        string scope,
        string? nonce,
        DateTimeOffset issuedAt,
        DateTimeOffset expiresAt)
    {
        Fingerprint = fingerprint;
        ClientId = clientId;
        Subject = subject;
        Session = session;
        Redirect = redirect;
        Challenge = challenge;
        ChallengeMethod = challengeMethod;
        Scope = scope;
        Nonce = nonce;
        IssuedAt = issuedAt;
        ExpiresAt = expiresAt;
    }

    /// <summary>What the code the client holds hashes to.</summary>
    public byte[] Fingerprint { get; }

    /// <summary>Which client it was issued to, and no other may present it.</summary>
    public string ClientId { get; }

    /// <summary>Whose account it stands for.</summary>
    public SubjectId Subject { get; }

    /// <summary>The session record the token will be minted from.</summary>
    public SessionId Session { get; }

    /// <summary>The destination it was issued against.</summary>
    public string Redirect { get; }

    /// <summary>What the verifier will be judged against.</summary>
    public string Challenge { get; }

    /// <summary>How the challenge was computed.</summary>
    public string ChallengeMethod { get; }

    /// <summary>What the exchange will cover.</summary>
    public string Scope { get; }

    /// <summary>What the identity token is to carry back, where the request carried one.</summary>
    public string? Nonce { get; }

    /// <summary>When it was issued.</summary>
    public DateTimeOffset IssuedAt { get; }

    /// <summary>When it stops being exchangeable.</summary>
    public DateTimeOffset ExpiresAt { get; }

    /// <summary>When it was exchanged, and nothing while it is unspent.</summary>
    public DateTimeOffset? SpentAt { get; private set; }

    /// <summary>
    /// Issues one.
    /// </summary>
    /// <param name="fingerprint">What the code hashes to.</param>
    /// <param name="client">Which client it is issued to.</param>
    /// <param name="subject">Whose account.</param>
    /// <param name="session">The session record it stands on.</param>
    /// <param name="redirect">The destination it is issued against.</param>
    /// <param name="challenge">What the verifier will be judged against.</param>
    /// <param name="challengeMethod">How the challenge was computed.</param>
    /// <param name="scope">What the exchange will cover.</param>
    /// <param name="nonce">What the identity token is to carry back, or nothing.</param>
    /// <param name="at">Now.</param>
    /// <param name="lifetime">How long it may be exchanged for.</param>
    /// <returns>The code.</returns>
    /// <exception cref="ArgumentNullException">The fingerprint or the client is absent.</exception>
    public static AuthorizationCode Issue(
        byte[] fingerprint,
        OidcClient client,
        SubjectId subject,
        SessionId session,
        string redirect,
        string challenge,
        string challengeMethod,
        string scope,
        string? nonce,
        DateTimeOffset at,
        TimeSpan lifetime)
    {
        ArgumentNullException.ThrowIfNull(fingerprint);
        ArgumentNullException.ThrowIfNull(client);

        return new AuthorizationCode(
            fingerprint,
            client.ClientId,
            subject,
            session,
            redirect,
            challenge,
            challengeMethod,
            scope,
            nonce,
            at,
            at + lifetime);
    }

    /// <summary>
    /// The code as the store holds it.
    /// </summary>
    /// <param name="fingerprint">What the code hashes to.</param>
    /// <param name="clientId">Which client holds it.</param>
    /// <param name="subject">Whose account.</param>
    /// <param name="session">The session record it stands on.</param>
    /// <param name="redirect">The destination it was issued against.</param>
    /// <param name="challenge">What the verifier is judged against.</param>
    /// <param name="challengeMethod">How the challenge was computed.</param>
    /// <param name="scope">What the exchange covers.</param>
    /// <param name="nonce">What the identity token is to carry back, or nothing.</param>
    /// <param name="issuedAt">When it was issued.</param>
    /// <param name="expiresAt">When it stops being exchangeable.</param>
    /// <param name="spentAt">When it was exchanged, or nothing.</param>
    /// <returns>The code.</returns>
    /// <exception cref="ArgumentNullException">The fingerprint is absent.</exception>
    public static AuthorizationCode Existing(
        byte[] fingerprint,
        string clientId,
        SubjectId subject,
        SessionId session,
        string redirect,
        string challenge,
        string challengeMethod,
        string scope,
        string? nonce,
        DateTimeOffset issuedAt,
        DateTimeOffset expiresAt,
        DateTimeOffset? spentAt)
    {
        ArgumentNullException.ThrowIfNull(fingerprint);

        return new AuthorizationCode(
            fingerprint,
            clientId,
            subject,
            session,
            redirect,
            challenge,
            challengeMethod,
            scope,
            nonce,
            issuedAt,
            expiresAt)
        {
            SpentAt = spentAt,
        };
    }

    /// <summary>
    /// Whether it may still be exchanged: unspent, unexpired, and by the client it was
    /// issued to at the destination it was issued against.
    /// </summary>
    /// <param name="clientId">Which client is presenting it.</param>
    /// <param name="redirect">What the presentation says the destination was.</param>
    /// <param name="now">Now.</param>
    /// <returns>Whether it opens anything.</returns>
    public bool Opens(string clientId, string redirect, DateTimeOffset now) =>
        SpentAt is null
        && now < ExpiresAt
        && string.Equals(ClientId, clientId, StringComparison.Ordinal)
        && string.Equals(Redirect, redirect, StringComparison.Ordinal);

    /// <summary>
    /// The code was exchanged, which is the last thing it does.
    /// </summary>
    /// <param name="at">When.</param>
    public void Spend(DateTimeOffset at) => SpentAt = at;
}
