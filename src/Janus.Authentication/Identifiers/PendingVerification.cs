using System;
using Janus.Authentication.Registration;
using Janus.Core;

namespace Janus.Authentication.Identifiers;

/// <summary>
/// An identifier of a live account waiting to be proved: the one an add staged
/// unverified, or the value a replace will swap in when it proves.
/// </summary>
/// <remarks>
/// Implements REG-IDENT-004, REG-IDENT-007 and REG-SESS-003. The verification itself
/// is the registration's, unchanged: the same code, the same link, the same five
/// tries, and the same rule that a link pressed anywhere but the browser that staged
/// it proves nothing. What this adds is whose it is, which browser staged it, and,
/// for a replace, whether the address being displaced has still to confirm.
/// </remarks>
internal sealed class PendingVerification
{
    private PendingVerification(
        SubjectId subject,
        SessionId? browser,
        StagedIdentity staged,
        bool isReplacement,
        bool oldMustConfirm,
        DateTimeOffset stagedAt)
    {
        Subject = subject;
        Browser = browser;
        Staged = staged;
        IsReplacement = isReplacement;
        OldMustConfirm = oldMustConfirm;
        StagedAt = stagedAt;
    }

    /// <summary>
    /// Whose identifier is waiting.
    /// </summary>
    public SubjectId Subject { get; }

    /// <summary>
    /// The session the add or the replace was made from, which is the only browser a
    /// press on the landing page proves anything in, and nothing where an enrolment
    /// session staged it and no browser holds a session at all (AUTH-RECOV-002).
    /// </summary>
    public SessionId? Browser { get; }

    /// <summary>
    /// The value being proved, with what was sent to prove it.
    /// </summary>
    public StagedIdentity Staged { get; }

    /// <summary>
    /// Whether proving it swaps the value of the identifier it names, rather than
    /// confirming the identifier itself.
    /// </summary>
    public bool IsReplacement { get; }

    /// <summary>
    /// Whether the address being displaced has to confirm before the swap applies,
    /// which is so only where the account has no other channel at all.
    /// </summary>
    public bool OldMustConfirm { get; }

    /// <summary>
    /// When the wait began.
    /// </summary>
    public DateTimeOffset StagedAt { get; }

    /// <summary>
    /// When the displaced address confirmed, where it was asked.
    /// </summary>
    public DateTimeOffset? OldConfirmedAt { get; private set; }

    /// <summary>
    /// The fingerprint of the token the displaced address was written to with, where
    /// it was asked and has not answered.
    /// </summary>
    public byte[]? OldLink { get; private set; }

    /// <summary>
    /// Which identifier of the account is waiting.
    /// </summary>
    public IdentifierId Identifier => Staged.Id;

    /// <summary>
    /// Whether everything the change waits on has happened.
    /// </summary>
    public bool IsSettled =>
        Staged.IsVerified && (!OldMustConfirm || OldConfirmedAt is not null);

    /// <summary>
    /// Stages the verification of an identifier the account has just taken on.
    /// </summary>
    /// <param name="subject">Whose it is.</param>
    /// <param name="browser">The session it was added from.</param>
    /// <param name="staged">The value being proved.</param>
    /// <param name="stagedAt">When the wait began.</param>
    /// <returns>The verification, with nothing sent yet.</returns>
    /// <exception cref="ArgumentNullException">The value is absent.</exception>
    public static PendingVerification ToAdd(
        SubjectId subject,
        SessionId? browser,
        StagedIdentity staged,
        DateTimeOffset stagedAt)
    {
        ArgumentNullException.ThrowIfNull(staged);

        return new PendingVerification(
            subject,
            browser,
            staged,
            isReplacement: false,
            oldMustConfirm: false,
            stagedAt);
    }

    /// <summary>
    /// Stages the verification of a value that will take the place of one the account
    /// already holds.
    /// </summary>
    /// <param name="subject">Whose it is.</param>
    /// <param name="browser">
    /// The session the replace was made from, or nothing where an enrolment session
    /// made it.
    /// </param>
    /// <param name="staged">The new value, under the identifier it will replace.</param>
    /// <param name="oldMustConfirm">
    /// Whether the address being displaced has to confirm, which is so only where the
    /// account has no other channel at all.
    /// </param>
    /// <param name="stagedAt">When the wait began.</param>
    /// <returns>The verification, with nothing sent yet.</returns>
    /// <exception cref="ArgumentNullException">The value is absent.</exception>
    public static PendingVerification ToReplace(
        SubjectId subject,
        SessionId? browser,
        StagedIdentity staged,
        bool oldMustConfirm,
        DateTimeOffset stagedAt)
    {
        ArgumentNullException.ThrowIfNull(staged);

        return new PendingVerification(
            subject,
            browser,
            staged,
            isReplacement: true,
            oldMustConfirm,
            stagedAt);
    }

    /// <summary>
    /// The verification as it already stands, which is the store translating a row and
    /// no step the person took.
    /// </summary>
    /// <param name="subject">Whose it is.</param>
    /// <param name="browser">The session it was staged from.</param>
    /// <param name="staged">The value being proved.</param>
    /// <param name="isReplacement">Whether proving it swaps a value.</param>
    /// <param name="oldMustConfirm">Whether the displaced address has to confirm.</param>
    /// <param name="oldConfirmedAt">When it confirmed, where it has.</param>
    /// <param name="oldLink">The fingerprint of the token it was written to with.</param>
    /// <param name="stagedAt">When the wait began.</param>
    /// <returns>The verification.</returns>
    /// <exception cref="ArgumentNullException">The value is absent.</exception>
    public static PendingVerification Existing(
        SubjectId subject,
        SessionId? browser,
        StagedIdentity staged,
        bool isReplacement,
        bool oldMustConfirm,
        DateTimeOffset? oldConfirmedAt,
        byte[]? oldLink,
        DateTimeOffset stagedAt)
    {
        ArgumentNullException.ThrowIfNull(staged);

        return new PendingVerification(subject, browser, staged, isReplacement, oldMustConfirm, stagedAt)
        {
            OldConfirmedAt = oldConfirmedAt,
            OldLink = oldLink,
        };
    }

    /// <summary>
    /// Records the link the displaced address was written to with.
    /// </summary>
    /// <param name="link">The fingerprint of the token the message carried.</param>
    /// <exception cref="ArgumentNullException">The fingerprint is absent.</exception>
    /// <exception cref="InvalidOperationException">It was not asked.</exception>
    public void AskedOld(byte[] link)
    {
        ArgumentNullException.ThrowIfNull(link);

        if (!OldMustConfirm)
        {
            throw new InvalidOperationException("The displaced address was not asked to confirm.");
        }

        OldLink = link;
    }

    /// <summary>
    /// Records that the address being displaced confirmed the change. The link it
    /// answered with is spent by the answer.
    /// </summary>
    /// <param name="at">When it confirmed.</param>
    /// <exception cref="InvalidOperationException">It was not asked.</exception>
    public void ConfirmOld(DateTimeOffset at)
    {
        if (!OldMustConfirm)
        {
            throw new InvalidOperationException("The displaced address was not asked to confirm.");
        }

        OldConfirmedAt = at;
        OldLink = null;
    }
}
