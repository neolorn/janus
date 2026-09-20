using System;
using System.Security.Cryptography;
using System.Text;
using Janus.Core;

namespace Janus.Authentication.Recovery;

/// <summary>
/// A credential its holder has said is gone: refused from the instant it was
/// reported, invalidated when the notified window ends, and cancellable from any
/// notification until then.
/// </summary>
/// <remarks>
/// Implements AUTH-RECOV-007 and D-141. The window is the control: long enough that
/// the real owner sees one of the notices, short enough that a locked-out person does
/// not give up. Nothing completes while no notice has reached anyone.
/// </remarks>
internal sealed class LossReport
{
    private LossReport(
        AuthenticatorId credential,
        SubjectId subject,
        byte[] cancel,
        DateTimeOffset reportedAt,
        DateTimeOffset invalidatesAt)
    {
        Credential = credential;
        Subject = subject;
        Cancel = cancel;
        ReportedAt = reportedAt;
        InvalidatesAt = invalidatesAt;
        NotifiedAt = reportedAt;
    }

    /// <summary>Which credential was reported.</summary>
    public AuthenticatorId Credential { get; }

    /// <summary>Whose it is.</summary>
    public SubjectId Subject { get; }

    /// <summary>
    /// The token every notification carries, held rather than fingerprinted because
    /// each repeat of the notice has to carry the same link (AUTH-RECOV-007). The
    /// store keeps it under the account's own key, where an erasure leaves it
    /// unreadable.
    /// </summary>
    public byte[] Cancel { get; }

    /// <summary>When it was reported.</summary>
    public DateTimeOffset ReportedAt { get; }

    /// <summary>When the window ends.</summary>
    public DateTimeOffset InvalidatesAt { get; }

    /// <summary>When the last notice went out.</summary>
    public DateTimeOffset NotifiedAt { get; private set; }

    /// <summary>
    /// Whether any notice has been taken by a transport, which is what invalidation
    /// waits for: a report nobody was told of completes nothing.
    /// </summary>
    public bool AnyDelivered { get; private set; }

    /// <summary>
    /// When invalidation fell due with no notice delivered, and nothing where that
    /// has not happened.
    /// </summary>
    public DateTimeOffset? HeldAt { get; private set; }

    /// <summary>
    /// Opens one.
    /// </summary>
    /// <param name="credential">Which credential.</param>
    /// <param name="subject">Whose it is.</param>
    /// <param name="cancel">The token every notification carries.</param>
    /// <param name="at">When it was reported.</param>
    /// <param name="window">How long the window runs for.</param>
    /// <returns>The report.</returns>
    public static LossReport Open(
        AuthenticatorId credential,
        SubjectId subject,
        OpaqueToken cancel,
        DateTimeOffset at,
        TimeSpan window) =>
        new(credential, subject, Encoding.UTF8.GetBytes(cancel.Value), at, at + window);

    /// <summary>
    /// The report as the store holds it.
    /// </summary>
    /// <param name="credential">Which credential.</param>
    /// <param name="subject">Whose it is.</param>
    /// <param name="cancel">The token every notification carries.</param>
    /// <param name="reportedAt">When it was reported.</param>
    /// <param name="invalidatesAt">When the window ends.</param>
    /// <param name="notifiedAt">When the last notice went out.</param>
    /// <param name="anyDelivered">Whether any notice was taken by a transport.</param>
    /// <param name="heldAt">When invalidation fell due undelivered, or nothing.</param>
    /// <returns>The report.</returns>
    /// <exception cref="ArgumentNullException">The token is absent.</exception>
    public static LossReport Existing(
        AuthenticatorId credential,
        SubjectId subject,
        byte[] cancel,
        DateTimeOffset reportedAt,
        DateTimeOffset invalidatesAt,
        DateTimeOffset notifiedAt,
        bool anyDelivered,
        DateTimeOffset? heldAt)
    {
        ArgumentNullException.ThrowIfNull(cancel);

        return new LossReport(credential, subject, cancel, reportedAt, invalidatesAt)
        {
            NotifiedAt = notifiedAt,
            AnyDelivered = anyDelivered,
            HeldAt = heldAt,
        };
    }

    /// <summary>
    /// Whether this is the token the notifications carried.
    /// </summary>
    /// <param name="presented">What was presented.</param>
    /// <returns>Whether it answers.</returns>
    public bool Matches(string? presented) =>
        presented is { Length: > 0 } carried
        && CryptographicOperations.FixedTimeEquals(Cancel, Encoding.UTF8.GetBytes(carried));

    /// <summary>
    /// Whether a notice is owed: one every interval across the window, and one a day
    /// before the window ends (AUTH-RECOV-007, D-153).
    /// </summary>
    /// <param name="now">Now.</param>
    /// <param name="interval">How often the notice repeats.</param>
    /// <returns>Whether one is owed.</returns>
    public bool NoticeDue(DateTimeOffset now, TimeSpan interval) =>
        now >= NotifiedAt + interval
        || (now >= InvalidatesAt - LastCall && NotifiedAt < InvalidatesAt - LastCall);

    /// <summary>
    /// A round of notices went out.
    /// </summary>
    /// <param name="delivered">Whether any of them was taken by a transport.</param>
    /// <param name="at">When.</param>
    public void Notified(bool delivered, DateTimeOffset at)
    {
        NotifiedAt = at;
        AnyDelivered = AnyDelivered || delivered;
    }

    /// <summary>
    /// The window ended with nothing delivered, so invalidation waits and the report
    /// carries that it did.
    /// </summary>
    /// <param name="at">When.</param>
    public void Hold(DateTimeOffset at) => HeldAt ??= at;

    private static readonly TimeSpan LastCall = TimeSpan.FromHours(24);
}
