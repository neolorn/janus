using System;
using Janus.Core;

namespace Janus.Authentication.Recovery;

/// <summary>
/// A time-boxed link that has gone out to a recorded channel: who it belongs to, what
/// it may do, and whether it has been spent.
/// </summary>
/// <remarks>
/// Implements AUTH-RECOV-002, AUTH-RECOV-005 and D-147. The row holds what the token
/// hashes to and never the token, so a dump of the table opens nothing. Spending an
/// enrolment link leaves the row standing as the enrolment session it opened, which
/// is what caps that session at the link's own lifetime.
/// </remarks>
internal sealed class RecoveryLink
{
    private RecoveryLink(
        byte[] fingerprint,
        SubjectId subject,
        RecoveryPurpose purpose,
        DateTimeOffset issuedAt,
        DateTimeOffset expiresAt,
        SubjectId? approver,
        bool mailboxLost)
    {
        Fingerprint = fingerprint;
        Subject = subject;
        Purpose = purpose;
        IssuedAt = issuedAt;
        ExpiresAt = expiresAt;
        Approver = approver;
        MailboxLost = mailboxLost;
    }

    /// <summary>What the token the message carried hashes to.</summary>
    public byte[] Fingerprint { get; }

    /// <summary>Whose account it opens.</summary>
    public SubjectId Subject { get; }

    /// <summary>What it may do.</summary>
    public RecoveryPurpose Purpose { get; }

    /// <summary>When it went out.</summary>
    public DateTimeOffset IssuedAt { get; }

    /// <summary>When it stops working.</summary>
    public DateTimeOffset ExpiresAt { get; }

    /// <summary>Who approved it, and nothing where the person asked for it themselves.</summary>
    public SubjectId? Approver { get; }

    /// <summary>
    /// Whether the approver recorded the account's mailbox as unreachable, which is
    /// what lets the session it opens settle a new address on the new address alone
    /// (AUTH-RECOV-002).
    /// </summary>
    public bool MailboxLost { get; }

    /// <summary>The session it opened, and nothing while it is unspent.</summary>
    public EnrolmentSessionId? Session { get; private set; }

    /// <summary>When it was spent, and nothing while it is unspent.</summary>
    public DateTimeOffset? SpentAt { get; private set; }

    /// <summary>
    /// Issues one.
    /// </summary>
    /// <param name="token">The secret the message carries.</param>
    /// <param name="subject">Whose account it opens.</param>
    /// <param name="purpose">What it may do.</param>
    /// <param name="at">Now.</param>
    /// <param name="lifetime">How long it works for.</param>
    /// <param name="approver">Who approved it, or nothing.</param>
    /// <param name="mailboxLost">Whether the approver recorded the mailbox as lost.</param>
    /// <returns>The link.</returns>
    public static RecoveryLink Issue(
        OpaqueToken token,
        SubjectId subject,
        RecoveryPurpose purpose,
        DateTimeOffset at,
        TimeSpan lifetime,
        SubjectId? approver = null,
        bool mailboxLost = false) =>
        new(token.Fingerprint(), subject, purpose, at, at + lifetime, approver, mailboxLost);

    /// <summary>
    /// The link as the store holds it.
    /// </summary>
    /// <param name="fingerprint">What the token hashes to.</param>
    /// <param name="subject">Whose account it opens.</param>
    /// <param name="purpose">What it may do.</param>
    /// <param name="issuedAt">When it went out.</param>
    /// <param name="expiresAt">When it stops working.</param>
    /// <param name="approver">Who approved it, or nothing.</param>
    /// <param name="mailboxLost">Whether the approver recorded the mailbox as lost.</param>
    /// <param name="session">The session it opened, or nothing.</param>
    /// <param name="spentAt">When it was spent, or nothing.</param>
    /// <returns>The link.</returns>
    /// <exception cref="ArgumentNullException">The fingerprint is absent.</exception>
    public static RecoveryLink Existing(
        byte[] fingerprint,
        SubjectId subject,
        RecoveryPurpose purpose,
        DateTimeOffset issuedAt,
        DateTimeOffset expiresAt,
        SubjectId? approver,
        bool mailboxLost,
        EnrolmentSessionId? session,
        DateTimeOffset? spentAt)
    {
        ArgumentNullException.ThrowIfNull(fingerprint);

        return new RecoveryLink(
            fingerprint,
            subject,
            purpose,
            issuedAt,
            expiresAt,
            approver,
            mailboxLost)
        {
            Session = session,
            SpentAt = spentAt,
        };
    }

    /// <summary>
    /// Whether it has stopped working.
    /// </summary>
    /// <param name="now">Now.</param>
    /// <returns>Whether it has expired.</returns>
    public bool HasExpired(DateTimeOffset now) => now >= ExpiresAt;

    /// <summary>
    /// Whether it still opens anything: unspent, unexpired and of the purpose the
    /// endpoint consuming it serves.
    /// </summary>
    /// <param name="purpose">What the endpoint consumes.</param>
    /// <param name="now">Now.</param>
    /// <returns>Whether it may be spent.</returns>
    public bool Opens(RecoveryPurpose purpose, DateTimeOffset now) =>
        Purpose == purpose && SpentAt is null && !HasExpired(now);

    /// <summary>
    /// The link was spent, which is the last thing it does.
    /// </summary>
    /// <param name="session">The enrolment session it opened, where it opened one.</param>
    /// <param name="at">When.</param>
    public void Spend(EnrolmentSessionId? session, DateTimeOffset at)
    {
        Session = session;
        SpentAt = at;
    }
}
