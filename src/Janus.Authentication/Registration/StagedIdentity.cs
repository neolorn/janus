using System;
using Janus.Core;

namespace Janus.Authentication.Registration;

/// <summary>
/// One identifier a registration session holds: the value in both forms, the link
/// sent to prove it, and whether it has been proved.
/// </summary>
/// <remarks>
/// Implements REG-SESS-001, REG-SESS-003, REG-SESS-004 and REG-IDENT-010. Nothing
/// here is reserved: the session holds the value and no other session is prevented
/// from holding it too. The code sent to prove it is not here: it is issued and
/// answered through its verification-code record (AUTH-FACT-004).
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
    /// The fingerprint of the link token last sent, absent where none is outstanding.
    /// </summary>
    [NeverLogged]
    public byte[]? Link { get; private set; }

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
    /// <param name="link">The fingerprint of the link token outstanding.</param>
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
        [NeverLogged] byte[]? link,
        DateTimeOffset? verifiedAt)
    {
        ArgumentNullException.ThrowIfNull(entered);
        ArgumentNullException.ThrowIfNull(canonical);

        return new StagedIdentity(id, kind, entered, canonical, isLocked, isExtra)
        {
            Link = link,
            VerifiedAt = verifiedAt,
        };
    }

    /// <summary>
    /// Records the link a message has just carried, in place of any before it. The
    /// code the same message carried is held by its verification-code record
    /// (AUTH-FACT-004).
    /// </summary>
    /// <param name="link">The fingerprint of the link token.</param>
    /// <exception cref="ArgumentNullException">The link is absent.</exception>
    public void Linked([NeverLogged] byte[] link)
    {
        ArgumentNullException.ThrowIfNull(link);

        Link = link;
    }

    /// <summary>
    /// Leaves no link standing: nothing was sent for the value as it now is.
    /// </summary>
    public void Unlinked() => Link = null;

    /// <summary>
    /// Records that a code or a same-browser link confirmed it. No link survives: it
    /// is spent by the verification it completed or stood beside.
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
        Link = null;
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
        Link = null;
    }
}
