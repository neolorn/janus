using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using Janus.Core;

namespace Janus.Authentication.SignIn;

/// <summary>
/// One sign-in in progress: who it resolved to, what has been presented so far and
/// the WebAuthn challenge it issued.
/// </summary>
/// <remarks>
/// Implements AUTH-ABUSE-001, AUTH-ABUSE-003, AUTH-FACT-014, AUTH-FACT-016 and
/// REG-DOM-001. A challenge exists for an identifier that resolves to nothing exactly as
/// for one that resolves to an account, because the two answers have to be the same; it
/// simply never accepts a factor. It carries the keyed hash of the identifier it was
/// opened with, so that a factor refused against it is counted against that identifier
/// whether or not an account holds it. Where it was opened with an email address the
/// account holds, it names that identifier, which is what a domain lock judges once a
/// factor has succeeded. It is bound, when it is opened, to the pre-authentication
/// session the browser carries, or to none, and a sign-in link's press and a fresh code
/// of the new-device check are judged against that binding (D-226).
/// </remarks>
internal sealed class Challenge
{
    private readonly List<Factor> _presented;

    private Challenge(
        byte[] fingerprint,
        SubjectId? subject,
        IdentifierId? email,
        byte[]? identifier,
        string webAuthn,
        byte[]? browser,
        DateTimeOffset createdAt,
        DateTimeOffset expiresAt,
        List<Factor> presented)
    {
        Fingerprint = fingerprint;
        Subject = subject;
        Email = email;
        Identifier = identifier;
        WebAuthn = webAuthn;
        Browser = browser;
        CreatedAt = createdAt;
        ExpiresAt = expiresAt;
        _presented = presented;
    }

    /// <summary>What the handle the caller holds hashes to.</summary>
    public byte[] Fingerprint { get; }

    /// <summary>
    /// Whose sign-in, or nothing where the identifier resolved to no account.
    /// </summary>
    public SubjectId? Subject { get; }

    /// <summary>
    /// The email address the sign-in was opened with, where the account holds it.
    /// </summary>
    public IdentifierId? Email { get; }

    /// <summary>
    /// The keyed hash of the identifier it was opened with, which the identifier
    /// component of the delay counts a refused factor against, or nothing where it was
    /// opened to restore a session, which names its account by the session alone.
    /// </summary>
    public byte[]? Identifier { get; }

    /// <summary>The value an assertion against this challenge has to sign over.</summary>
    public string WebAuthn { get; }

    /// <summary>
    /// What the pre-authentication session of the browser that opened it hashes to, or
    /// nothing where that browser carried none.
    /// </summary>
    [NeverLogged]
    public byte[]? Browser { get; }

    /// <summary>When it opened.</summary>
    public DateTimeOffset CreatedAt { get; }

    /// <summary>When it stops being answerable.</summary>
    public DateTimeOffset ExpiresAt { get; private set; }

    /// <summary>
    /// When the new-device check held the sign-in, or nothing where it has not.
    /// </summary>
    public DateTimeOffset? HeldAt { get; private set; }

    /// <summary>What has been presented against it and accepted.</summary>
    public IReadOnlyList<Factor> Presented => _presented;

    /// <summary>
    /// Opens one.
    /// </summary>
    /// <param name="handle">The secret the caller presents at every later step.</param>
    /// <param name="subject">Whose sign-in, or nothing.</param>
    /// <param name="email">The email it was opened with, where the account holds it.</param>
    /// <param name="identifier">
    /// The keyed hash of the identifier it was opened with, or nothing for a restore.
    /// </param>
    /// <param name="webAuthn">The WebAuthn challenge issued with it.</param>
    /// <param name="browser">
    /// What the opening browser's pre-authentication session hashes to, or nothing.
    /// </param>
    /// <param name="at">Now.</param>
    /// <param name="lifetime">How long it answers for.</param>
    /// <returns>The challenge.</returns>
    public static Challenge Open(
        OpaqueToken handle,
        SubjectId? subject,
        IdentifierId? email,
        byte[]? identifier,
        string webAuthn,
        [NeverLogged] byte[]? browser,
        DateTimeOffset at,
        TimeSpan lifetime) =>
        new(handle.Fingerprint(), subject, email, identifier, webAuthn, browser, at, at + lifetime, []);

    /// <summary>
    /// The challenge as the store holds it.
    /// </summary>
    /// <param name="fingerprint">What the handle hashes to.</param>
    /// <param name="subject">Whose sign-in, or nothing.</param>
    /// <param name="email">The email it was opened with, where the account holds it.</param>
    /// <param name="identifier">
    /// The keyed hash of the identifier it was opened with, or nothing for a restore.
    /// </param>
    /// <param name="webAuthn">The WebAuthn challenge issued with it.</param>
    /// <param name="browser">What the opening browser's pre-authentication session hashes to, or nothing.</param>
    /// <param name="createdAt">When it opened.</param>
    /// <param name="expiresAt">When it stops answering.</param>
    /// <param name="heldAt">When the new-device check held it, or nothing.</param>
    /// <param name="presented">What has been accepted against it.</param>
    /// <returns>The challenge.</returns>
    /// <exception cref="ArgumentNullException">A part is absent.</exception>
    public static Challenge Existing(
        byte[] fingerprint,
        SubjectId? subject,
        IdentifierId? email,
        byte[]? identifier,
        string webAuthn,
        [NeverLogged] byte[]? browser,
        DateTimeOffset createdAt,
        DateTimeOffset expiresAt,
        DateTimeOffset? heldAt,
        IReadOnlyCollection<Factor> presented)
    {
        ArgumentNullException.ThrowIfNull(fingerprint);
        ArgumentNullException.ThrowIfNull(presented);

        return new Challenge(
            fingerprint,
            subject,
            email,
            identifier,
            webAuthn,
            browser,
            createdAt,
            expiresAt,
            [.. presented])
        {
            HeldAt = heldAt,
        };
    }

    /// <summary>
    /// Whether it has stopped answering.
    /// </summary>
    /// <param name="now">Now.</param>
    /// <returns>Whether it has expired.</returns>
    public bool HasExpired(DateTimeOffset now) => now >= ExpiresAt;

    /// <summary>
    /// Whether a request carrying this pre-authentication session is the browser it is
    /// bound to; one bound to none is no request's (D-226).
    /// </summary>
    /// <param name="presented">What the request's pre-authentication session hashes to, or nothing.</param>
    /// <returns>Whether it is bound to it.</returns>
    public bool BoundTo([NeverLogged] byte[]? presented) =>
        Browser is not null
        && presented is not null
        && CryptographicOperations.FixedTimeEquals(Browser, presented);

    /// <summary>
    /// Keeps it answering at least until an instant, a sign-in link issued on it living
    /// that long (D-226).
    /// </summary>
    /// <param name="until">The instant.</param>
    public void Outlive(DateTimeOffset until)
    {
        if (until > ExpiresAt)
        {
            ExpiresAt = until;
        }
    }

    /// <summary>
    /// Records that the new-device check held the sign-in, which a fresh code of the
    /// check is asked on (AUTH-FACT-016, D-224).
    /// </summary>
    /// <param name="at">Now.</param>
    public void Held(DateTimeOffset at) => HeldAt ??= at;

    /// <summary>
    /// Records a factor accepted against it.
    /// </summary>
    /// <param name="factor">What was accepted.</param>
    public void Accepted(Factor factor)
    {
        if (!_presented.Contains(factor))
        {
            _presented.Add(factor);
        }
    }

}
