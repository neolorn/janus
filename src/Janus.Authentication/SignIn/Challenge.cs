using System;
using System.Collections.Generic;
using Janus.Core;

namespace Janus.Authentication.SignIn;

/// <summary>
/// One sign-in in progress: who it resolved to, what has been presented so far and
/// the WebAuthn challenge it issued.
/// </summary>
/// <remarks>
/// Implements AUTH-ABUSE-003, AUTH-FACT-014, AUTH-FACT-016 and REG-DOM-001. A challenge
/// exists for an identifier that resolves to nothing exactly as for one that resolves
/// to an account, because the two answers have to be the same; it simply never accepts
/// a factor. Where it was opened with an email address the account holds, it names that
/// identifier, which is what a domain lock judges once a factor has succeeded.
/// </remarks>
internal sealed class Challenge
{
    private readonly List<Factor> _presented;

    private Challenge(
        byte[] fingerprint,
        SubjectId? subject,
        IdentifierId? email,
        string webAuthn,
        DateTimeOffset createdAt,
        DateTimeOffset expiresAt,
        List<Factor> presented)
    {
        Fingerprint = fingerprint;
        Subject = subject;
        Email = email;
        WebAuthn = webAuthn;
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

    /// <summary>The value an assertion against this challenge has to sign over.</summary>
    public string WebAuthn { get; }

    /// <summary>When it opened.</summary>
    public DateTimeOffset CreatedAt { get; }

    /// <summary>When it stops being answerable.</summary>
    public DateTimeOffset ExpiresAt { get; }

    /// <summary>What has been presented against it and accepted.</summary>
    public IReadOnlyList<Factor> Presented => _presented;

    /// <summary>
    /// Opens one.
    /// </summary>
    /// <param name="handle">The secret the caller presents at every later step.</param>
    /// <param name="subject">Whose sign-in, or nothing.</param>
    /// <param name="email">The email it was opened with, where the account holds it.</param>
    /// <param name="webAuthn">The WebAuthn challenge issued with it.</param>
    /// <param name="at">Now.</param>
    /// <param name="lifetime">How long it answers for.</param>
    /// <returns>The challenge.</returns>
    public static Challenge Open(
        OpaqueToken handle,
        SubjectId? subject,
        IdentifierId? email,
        string webAuthn,
        DateTimeOffset at,
        TimeSpan lifetime) =>
        new(handle.Fingerprint(), subject, email, webAuthn, at, at + lifetime, []);

    /// <summary>
    /// The challenge as the store holds it.
    /// </summary>
    /// <param name="fingerprint">What the handle hashes to.</param>
    /// <param name="subject">Whose sign-in, or nothing.</param>
    /// <param name="email">The email it was opened with, where the account holds it.</param>
    /// <param name="webAuthn">The WebAuthn challenge issued with it.</param>
    /// <param name="createdAt">When it opened.</param>
    /// <param name="expiresAt">When it stops answering.</param>
    /// <param name="presented">What has been accepted against it.</param>
    /// <returns>The challenge.</returns>
    /// <exception cref="ArgumentNullException">A part is absent.</exception>
    public static Challenge Existing(
        byte[] fingerprint,
        SubjectId? subject,
        IdentifierId? email,
        string webAuthn,
        DateTimeOffset createdAt,
        DateTimeOffset expiresAt,
        IReadOnlyCollection<Factor> presented)
    {
        ArgumentNullException.ThrowIfNull(fingerprint);
        ArgumentNullException.ThrowIfNull(presented);

        return new Challenge(fingerprint, subject, email, webAuthn, createdAt, expiresAt, [.. presented]);
    }

    /// <summary>
    /// Whether it has stopped answering.
    /// </summary>
    /// <param name="now">Now.</param>
    /// <returns>Whether it has expired.</returns>
    public bool HasExpired(DateTimeOffset now) => now >= ExpiresAt;

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
