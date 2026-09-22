using System;
using Janus.Core;

namespace Janus.Privacy.Requests;

/// <summary>
/// One data subject request on the queue, with the three instants the clock produces:
/// when it is warned about, when it is escalated, and when it is due.
/// </summary>
/// <remarks>
/// Implements PRIV-RIGHT-001, PRIV-RIGHT-002 and D-136. The three instants are
/// computed once, at entry, on the calendar as it stood then. A holiday announced
/// afterwards is honoured because a request already counted keeps the earlier
/// deadline, and an earlier deadline is always compliant.
/// </remarks>
internal sealed class QueuedRequest
{
    private QueuedRequest(
        PrivacyRequestId id,
        SubjectId subject,
        PrivacyRequestType type,
        string detail,
        DateOnly receivedAt,
        DateTimeOffset createdAt,
        Deadline deadline,
        string? channel,
        string? identityConfirmation)
    {
        Id = id;
        Subject = subject;
        Type = type;
        Detail = detail;
        ReceivedAt = receivedAt;
        CreatedAt = createdAt;
        ReceiptSentAt = createdAt;
        DecisionDue = deadline.Due;
        WarnAt = deadline.WarnAt;
        EscalateAt = deadline.EscalateAt;
        Status = PrivacyRequestStatus.Open;
        Channel = channel;
        IdentityConfirmation = identityConfirmation;
    }

    /// <summary>What the request is held under.</summary>
    public PrivacyRequestId Id { get; }

    /// <summary>Whose request it is.</summary>
    public SubjectId Subject { get; }

    /// <summary>What it asks for.</summary>
    public PrivacyRequestType Type { get; }

    /// <summary>What the subject, or the human entering it, wrote.</summary>
    public string Detail { get; }

    /// <summary>The calendar date it reached the company.</summary>
    public DateOnly ReceivedAt { get; }

    /// <summary>When it entered the queue.</summary>
    public DateTimeOffset CreatedAt { get; }

    /// <summary>When the automatic receipt went out, which is entry.</summary>
    public DateTimeOffset ReceiptSentAt { get; }

    /// <summary>The end of the sixth working day after submission.</summary>
    public DateTimeOffset DecisionDue { get; }

    /// <summary>The start of the working day the warning is due on.</summary>
    public DateTimeOffset WarnAt { get; }

    /// <summary>Midnight at the head of the deadline day.</summary>
    public DateTimeOffset EscalateAt { get; }

    /// <summary>Where it stands.</summary>
    public PrivacyRequestStatus Status { get; private set; }

    /// <summary>When it was decided, where it was.</summary>
    public DateTimeOffset? DecidedAt { get; private set; }

    /// <summary>Why it was refused, where it was.</summary>
    public string? DecisionReason { get; private set; }

    /// <summary>How an out-of-band request arrived, where it did.</summary>
    public string? Channel { get; }

    /// <summary>What confirmed the requester was the subject, out of band.</summary>
    public string? IdentityConfirmation { get; }

    /// <summary>When the approaching-deadline alert was raised, where it was.</summary>
    public DateTimeOffset? WarnedAt { get; private set; }

    /// <summary>When the deadline-day alert was raised, where it was.</summary>
    public DateTimeOffset? EscalatedAt { get; private set; }

    /// <summary>Whether a human has yet to decide it.</summary>
    public bool Open => Status is PrivacyRequestStatus.Open;

    /// <summary>
    /// A request the subject submitted for themselves, received today.
    /// </summary>
    /// <param name="subject">Whose it is.</param>
    /// <param name="type">What it asks for.</param>
    /// <param name="detail">What they wrote.</param>
    /// <param name="receivedAt">Today, in the zone the deployment named.</param>
    /// <param name="createdAt">When it entered the queue.</param>
    /// <param name="deadline">The clock it is decided against.</param>
    /// <returns>The request.</returns>
    public static QueuedRequest Submitted(
        SubjectId subject,
        PrivacyRequestType type,
        string detail,
        DateOnly receivedAt,
        DateTimeOffset createdAt,
        Deadline deadline) =>
        new(
            PrivacyRequestId.Of(createdAt),
            subject,
            type,
            detail,
            receivedAt,
            createdAt,
            deadline,
            channel: null,
            identityConfirmation: null);

    /// <summary>
    /// A request an authorised human entered on a subject's behalf.
    /// </summary>
    /// <param name="entry">The request as it arrived.</param>
    /// <param name="createdAt">When it entered the queue.</param>
    /// <param name="deadline">The clock it is decided against.</param>
    /// <returns>The request.</returns>
    /// <exception cref="ArgumentNullException">The entry is absent.</exception>
    public static QueuedRequest Entered(
        PrivacyRequestEntry entry,
        DateTimeOffset createdAt,
        Deadline deadline)
    {
        ArgumentNullException.ThrowIfNull(entry);

        return new QueuedRequest(
            PrivacyRequestId.Of(createdAt),
            entry.Subject,
            entry.Type,
            entry.Detail,
            entry.ReceivedAt,
            createdAt,
            deadline,
            entry.Channel,
            entry.IdentityConfirmation);
    }

    /// <summary>
    /// The request as the row already holds it.
    /// </summary>
    /// <param name="held">Every field of the row.</param>
    /// <returns>The request.</returns>
    /// <exception cref="ArgumentNullException">The row is absent.</exception>
    public static QueuedRequest Existing(HeldRequest held)
    {
        ArgumentNullException.ThrowIfNull(held);

        return new QueuedRequest(
            held.Id,
            held.Subject,
            held.Type,
            held.Detail,
            held.ReceivedAt,
            held.CreatedAt,
            new Deadline(held.DecisionDue, held.WarnAt, held.EscalateAt),
            held.Channel,
            held.IdentityConfirmation)
        {
            Status = held.Status,
            DecidedAt = held.DecidedAt,
            DecisionReason = held.DecisionReason,
            WarnedAt = held.WarnedAt,
            EscalatedAt = held.EscalatedAt,
        };
    }

    /// <summary>
    /// A human decided to do what was asked.
    /// </summary>
    /// <param name="at">When they decided.</param>
    /// <exception cref="InvalidOperationException">The request was decided already.</exception>
    public void Fulfil(DateTimeOffset at) => Decide(PrivacyRequestStatus.Fulfilled, at, reason: null);

    /// <summary>
    /// A human decided not to, and said why.
    /// </summary>
    /// <param name="at">When they decided.</param>
    /// <param name="reason">Why.</param>
    /// <exception cref="InvalidOperationException">The request was decided already.</exception>
    public void Refuse(DateTimeOffset at, string reason) =>
        Decide(PrivacyRequestStatus.Refused, at, reason);

    /// <summary>
    /// The deadline passed on a restriction, which is granted: restriction suspends
    /// action and never visibility, so granting it is always safe (PRIV-RIGHT-004).
    /// </summary>
    /// <param name="at">When the lapse was recorded.</param>
    /// <exception cref="InvalidOperationException">The request was decided already.</exception>
    public void GrantByLapse(DateTimeOffset at) =>
        Decide(PrivacyRequestStatus.GrantedByLapse, at, reason: null);

    /// <summary>
    /// The deadline passed on a request the system cannot grant by itself, which the
    /// statute deems a rejection.
    /// </summary>
    /// <param name="at">When the lapse was recorded.</param>
    /// <exception cref="InvalidOperationException">The request was decided already.</exception>
    public void DeemRefusedByLapse(DateTimeOffset at) =>
        Decide(PrivacyRequestStatus.DeemedRefusedByLapse, at, reason: null);

    /// <summary>
    /// The approaching-deadline alert was raised, and is not raised again.
    /// </summary>
    /// <param name="at">When it was raised.</param>
    public void Warned(DateTimeOffset at) => WarnedAt = at;

    /// <summary>
    /// The deadline-day alert was raised, and is not raised again.
    /// </summary>
    /// <param name="at">When it was raised.</param>
    public void Escalated(DateTimeOffset at) => EscalatedAt = at;

    /// <summary>
    /// The request as the queue shows it.
    /// </summary>
    /// <returns>The read shape.</returns>
    public PrivacyRequest Read() =>
        new(
            Id,
            Subject,
            Type,
            Detail,
            ReceivedAt,
            CreatedAt,
            ReceiptSentAt,
            DecisionDue,
            Status,
            DecidedAt,
            DecisionReason,
            Channel,
            IdentityConfirmation);

    private void Decide(PrivacyRequestStatus status, DateTimeOffset at, string? reason)
    {
        if (!Open)
        {
            throw new InvalidOperationException("The request was decided already.");
        }

        Status = status;
        DecidedAt = at;
        DecisionReason = reason;
    }
}
