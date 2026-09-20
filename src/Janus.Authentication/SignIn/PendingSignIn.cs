using System;
using System.Security.Cryptography;
using Janus.Core;

namespace Janus.Authentication.SignIn;

/// <summary>
/// A sign-in link or code that has gone out and not yet been used: who it belongs to,
/// what kind of factor it stands for, the code it also carries, and the browser that
/// asked for it.
/// </summary>
/// <remarks>
/// Implements AUTH-FACT-003 and REG-SESS-003. A link completes only in the browser
/// that requested it and only on a press; the code is what the person types where the
/// sign-in began when they opened it somewhere else.
/// </remarks>
internal sealed class PendingSignIn
{
    private PendingSignIn(
        byte[] fingerprint,
        SubjectId subject,
        Factor factor,
        byte[] code,
        byte[]? browser,
        DateTimeOffset issuedAt,
        DateTimeOffset expiresAt)
    {
        Fingerprint = fingerprint;
        Subject = subject;
        Factor = factor;
        Code = code;
        Browser = browser;
        IssuedAt = issuedAt;
        ExpiresAt = expiresAt;
    }

    /// <summary>What the token carried by the message hashes to.</summary>
    public byte[] Fingerprint { get; }

    /// <summary>Whose sign-in.</summary>
    public SubjectId Subject { get; }

    /// <summary>Which factor it stands for.</summary>
    public Factor Factor { get; }

    /// <summary>The code the same message carried, held as it is compared.</summary>
    public byte[] Code { get; }

    /// <summary>
    /// What the requesting browser carried, or nothing where it carried none.
    /// </summary>
    public byte[]? Browser { get; }

    /// <summary>When it went out.</summary>
    public DateTimeOffset IssuedAt { get; }

    /// <summary>When it stops working.</summary>
    public DateTimeOffset ExpiresAt { get; }

    /// <summary>How many wrong codes have been typed against it.</summary>
    public int WrongAttempts { get; private set; }

    /// <summary>
    /// Issues one.
    /// </summary>
    /// <param name="token">The secret the message carries.</param>
    /// <param name="subject">Whose sign-in.</param>
    /// <param name="factor">Which factor it stands for.</param>
    /// <param name="code">The code the same message carries.</param>
    /// <param name="browser">What the requesting browser carried, or nothing.</param>
    /// <param name="at">Now.</param>
    /// <param name="lifetime">How long it works for.</param>
    /// <returns>The pending sign-in.</returns>
    public static PendingSignIn Issue(
        OpaqueToken token,
        SubjectId subject,
        Factor factor,
        string code,
        byte[]? browser,
        DateTimeOffset at,
        TimeSpan lifetime) =>
        new(token.Fingerprint(), subject, factor, Held(code), browser, at, at + lifetime);

    /// <summary>
    /// The pending sign-in as the store holds it.
    /// </summary>
    /// <param name="fingerprint">What the token hashes to.</param>
    /// <param name="subject">Whose sign-in.</param>
    /// <param name="factor">Which factor it stands for.</param>
    /// <param name="code">The code it carries.</param>
    /// <param name="browser">What the requesting browser carried, or nothing.</param>
    /// <param name="issuedAt">When it went out.</param>
    /// <param name="expiresAt">When it stops working.</param>
    /// <param name="wrongAttempts">Wrong codes typed against it.</param>
    /// <returns>The pending sign-in.</returns>
    /// <exception cref="ArgumentNullException">A part is absent.</exception>
    public static PendingSignIn Existing(
        byte[] fingerprint,
        SubjectId subject,
        Factor factor,
        byte[] code,
        byte[]? browser,
        DateTimeOffset issuedAt,
        DateTimeOffset expiresAt,
        int wrongAttempts)
    {
        ArgumentNullException.ThrowIfNull(fingerprint);
        ArgumentNullException.ThrowIfNull(code);

        return new PendingSignIn(fingerprint, subject, factor, code, browser, issuedAt, expiresAt)
        {
            WrongAttempts = wrongAttempts,
        };
    }

    /// <summary>
    /// Whether it has stopped working.
    /// </summary>
    /// <param name="now">Now.</param>
    /// <returns>Whether it has expired.</returns>
    public bool HasExpired(DateTimeOffset now) => now >= ExpiresAt;

    /// <summary>
    /// Whether this browser is the one that asked for it.
    /// </summary>
    /// <param name="presented">What the asking browser carries, or nothing.</param>
    /// <returns>Whether the two are the same browser.</returns>
    public bool SameBrowser(byte[]? presented) =>
        Browser is not null
        && presented is not null
        && CryptographicOperations.FixedTimeEquals(Browser, presented);

    /// <summary>
    /// Whether this is the code the message carried.
    /// </summary>
    /// <param name="entered">What was typed.</param>
    /// <returns>Whether it matches.</returns>
    public bool Matches(string entered) => Registration.VerificationCode.Matches(Code, entered);

    /// <summary>
    /// A wrong code was typed against it.
    /// </summary>
    public void Missed() => WrongAttempts++;

    private static byte[] Held(string code) => Registration.VerificationCode.Held(code);
}
