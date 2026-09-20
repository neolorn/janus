using System;

namespace Janus.Core;

/// <summary>
/// One data subject request on the queue, with the clock it is decided against.
/// </summary>
/// <param name="Id">What the request is held under.</param>
/// <param name="Subject">Whose request it is.</param>
/// <param name="Type">What it asks for.</param>
/// <param name="Detail">What the subject or the human entering it wrote.</param>
/// <param name="ReceivedAt">
/// The calendar date the request reached the company, in
/// <c>privacy.calendar.timezone</c>. The decision clock runs from the end of it.
/// </param>
/// <param name="CreatedAt">When the request entered the queue.</param>
/// <param name="ReceiptSentAt">When the automatic receipt went out, which is creation.</param>
/// <param name="DecisionDue">The end of the sixth working day after submission.</param>
/// <param name="Status">Where it stands.</param>
/// <param name="DecidedAt">When it was decided, where it was.</param>
/// <param name="DecisionReason">Why it was refused, where it was.</param>
/// <param name="Channel">How an out-of-band request arrived, where it did.</param>
/// <param name="IdentityConfirmation">
/// What the human did to confirm the requester was the subject, for an out-of-band
/// request.
/// </param>
/// <remarks>
/// Implements PRIV-RIGHT-001, PRIV-RIGHT-002 and chapter 09 section 7. The deadline
/// is computed once, at entry, on the calendar as it stood then; a holiday announced
/// later is honoured because the sweep recounts nothing and the deadline it holds is
/// never later than a recount would give.
/// </remarks>
public sealed record PrivacyRequest(
    PrivacyRequestId Id,
    SubjectId Subject,
    PrivacyRequestType Type,
    string Detail,
    DateOnly ReceivedAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset ReceiptSentAt,
    DateTimeOffset DecisionDue,
    PrivacyRequestStatus Status,
    DateTimeOffset? DecidedAt,
    string? DecisionReason,
    string? Channel,
    string? IdentityConfirmation)
{
    /// <summary>
    /// Whether the request is still waiting on a human.
    /// </summary>
    public bool Open => Status is PrivacyRequestStatus.Open;
}
