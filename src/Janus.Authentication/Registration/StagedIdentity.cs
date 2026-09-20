using System;
using System.Security.Cryptography;
using Janus.Core;

namespace Janus.Authentication.Registration;

/// <summary>
/// One identifier a registration session holds: the value in both forms, what was
/// sent to prove it, and whether it has been proved.
/// </summary>
/// <remarks>
/// Implements REG-SESS-001, REG-SESS-003, REG-SESS-004 and REG-IDENT-010. Nothing
/// here is reserved: the session holds the value and no other session is prevented
/// from holding it too.
/// </remarks>
internal sealed class StagedIdentity
{
    private StagedIdentity(
        IdentifierId id,
        IdentifierKind kind,
        string entered,
        string canonical,
        bool isLocked,
        bool isExtra)
    {
        Id = id;
        Kind = kind;
        Entered = entered;
        Canonical = canonical;
        IsLocked = isLocked;
        IsExtra = isExtra;
    }

    /// <summary>
    /// Which staged identifier, as the verify and change endpoints name it.
    /// </summary>
    public IdentifierId Id { get; }

    /// <summary>
    /// Whether it is an email or a phone.
    /// </summary>
    public IdentifierKind Kind { get; }

    /// <summary>
    /// The form the person entered, which is what is shown back to them.
    /// </summary>
    public string Entered { get; private set; }

    /// <summary>
    /// The form it is compared and sent to under.
    /// </summary>
    public string Canonical { get; private set; }

    /// <summary>
    /// Whether it is fixed against change: an invitation bound it, or a provider
    /// operates the mailbox.
    /// </summary>
    public bool IsLocked { get; }

    /// <summary>
    /// Whether it was added at the confirm step rather than by the step that collects
    /// its kind, which is what makes it discardable.
    /// </summary>
    public bool IsExtra { get; }

    /// <summary>
    /// The code last sent, absent where none is outstanding. It is held rather than
    /// fingerprinted because a link opened elsewhere shows it (REG-SESS-003).
    /// </summary>
    public byte[]? Code { get; private set; }

    /// <summary>
    /// When the code stops being accepted.
    /// </summary>
    public DateTimeOffset? CodeExpiresAt { get; private set; }

    /// <summary>
    /// The fingerprint of the link token last sent, absent where none is outstanding.
    /// </summary>
    public byte[]? Link { get; private set; }

    /// <summary>
    /// How many wrong codes have been presented since the code was sent.
    /// </summary>
    public int WrongAttempts { get; private set; }

    /// <summary>
    /// Whether the code has been invalidated by wrong tries, after which even the
    /// right one is refused.
    /// </summary>
    public bool CodeSpent { get; private set; }

    /// <summary>
    /// When a code or a same-browser link confirmed it.
    /// </summary>
    public DateTimeOffset? VerifiedAt { get; private set; }

    /// <summary>
    /// Whether it counts.
    /// </summary>
    public bool IsVerified => VerifiedAt is not null;

    /// <summary>
    /// Stages a value the person entered.
    /// </summary>
    /// <param name="id">The identifier issued for it.</param>
    /// <param name="kind">Which kind it is.</param>
    /// <param name="entered">The value as entered.</param>
    /// <param name="canonical">The value in its canonical form.</param>
    /// <param name="isLocked">Whether it is fixed against change.</param>
    /// <param name="isExtra">Whether it was added at the confirm step.</param>
    /// <returns>The staged identifier, unverified and with nothing sent yet.</returns>
    /// <exception cref="ArgumentNullException">Either form is absent.</exception>
    public static StagedIdentity Of(
        IdentifierId id,
        IdentifierKind kind,
        string entered,
        string canonical,
        bool isLocked = false,
        bool isExtra = false)
    {
        ArgumentNullException.ThrowIfNull(entered);
        ArgumentNullException.ThrowIfNull(canonical);

        return new StagedIdentity(id, kind, entered, canonical, isLocked, isExtra);
    }

    /// <summary>
    /// The staged identifier as it already stands, which is the store translating a
    /// row and no step the person took.
    /// </summary>
    /// <param name="id">Which staged identifier.</param>
    /// <param name="kind">Which kind it is.</param>
    /// <param name="entered">The value as entered.</param>
    /// <param name="canonical">The value in its canonical form.</param>
    /// <param name="isLocked">Whether it is fixed against change.</param>
    /// <param name="isExtra">Whether it was added at the confirm step.</param>
    /// <param name="code">The code outstanding.</param>
    /// <param name="codeExpiresAt">When that code stops being accepted.</param>
    /// <param name="link">The fingerprint of the link token outstanding.</param>
    /// <param name="wrongAttempts">How many wrong codes have been presented.</param>
    /// <param name="codeSpent">Whether the code has been invalidated.</param>
    /// <param name="verifiedAt">When it was confirmed, where it has been.</param>
    /// <returns>The staged identifier.</returns>
    /// <exception cref="ArgumentNullException">Either form is absent.</exception>
    public static StagedIdentity Existing(
        IdentifierId id,
        IdentifierKind kind,
        string entered,
        string canonical,
        bool isLocked,
        bool isExtra,
        byte[]? code,
        DateTimeOffset? codeExpiresAt,
        byte[]? link,
        int wrongAttempts,
        bool codeSpent,
        DateTimeOffset? verifiedAt)
    {
        ArgumentNullException.ThrowIfNull(entered);
        ArgumentNullException.ThrowIfNull(canonical);

        return new StagedIdentity(id, kind, entered, canonical, isLocked, isExtra)
        {
            Code = code,
            CodeExpiresAt = codeExpiresAt,
            Link = link,
            WrongAttempts = wrongAttempts,
            CodeSpent = codeSpent,
            VerifiedAt = verifiedAt,
        };
    }

    /// <summary>
    /// Records the code and the link a message has just carried, replacing whatever
    /// was outstanding.
    /// </summary>
    /// <param name="code">The code sent.</param>
    /// <param name="link">The fingerprint of the link token.</param>
    /// <param name="expiresAt">When both stop being accepted.</param>
    /// <exception cref="ArgumentNullException">Either is absent.</exception>
    public void Sent(byte[] code, byte[] link, DateTimeOffset expiresAt)
    {
        ArgumentNullException.ThrowIfNull(code);
        ArgumentNullException.ThrowIfNull(link);

        Forget();

        Code = code;
        Link = link;
        CodeExpiresAt = expiresAt;
        WrongAttempts = 0;
        CodeSpent = false;
    }

    /// <summary>
    /// Records a wrong code, which invalidates the code once the cap is reached.
    /// </summary>
    /// <param name="cap">How many wrong tries the code survives.</param>
    public void Missed(int cap)
    {
        WrongAttempts++;

        if (WrongAttempts >= cap)
        {
            CodeSpent = true;
        }
    }

    /// <summary>
    /// Records that a code or a same-browser link confirmed it. Nothing outstanding
    /// survives: the code and the link are spent by the verification they completed.
    /// </summary>
    /// <param name="at">When it was confirmed.</param>
    /// <exception cref="InvalidOperationException">It is verified already.</exception>
    public void Verify(DateTimeOffset at)
    {
        if (IsVerified)
        {
            throw new InvalidOperationException("The identifier is verified already.");
        }

        VerifiedAt = at;
        Forget();
    }

    /// <summary>
    /// Corrects the value in place, which discards the verification and everything
    /// outstanding against the old one.
    /// </summary>
    /// <param name="entered">The corrected value as entered.</param>
    /// <param name="canonical">The corrected value in its canonical form.</param>
    /// <exception cref="ArgumentNullException">Either form is absent.</exception>
    /// <exception cref="InvalidOperationException">It is locked.</exception>
    public void Change(string entered, string canonical)
    {
        ArgumentNullException.ThrowIfNull(entered);
        ArgumentNullException.ThrowIfNull(canonical);

        if (IsLocked)
        {
            throw new InvalidOperationException("A locked identifier is not changed.");
        }

        Entered = entered;
        Canonical = canonical;
        VerifiedAt = null;
        WrongAttempts = 0;
        CodeSpent = false;
        Forget();
    }

    // The code is a secret for as long as it is outstanding and no longer.
    private void Forget()
    {
        if (Code is not null)
        {
            CryptographicOperations.ZeroMemory(Code);
        }

        Code = null;
        Link = null;
        CodeExpiresAt = null;
    }
}
