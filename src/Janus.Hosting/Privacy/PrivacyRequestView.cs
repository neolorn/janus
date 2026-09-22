using System;
using Janus.Core;

namespace Janus.Hosting.Privacy;

/// <summary>
/// One request as the queue shows it.
/// </summary>
/// <param name="RequestId">What it is held under.</param>
/// <param name="Subject">Whose it is.</param>
/// <param name="Type">What it asks for.</param>
/// <param name="Detail">What was written.</param>
/// <param name="ReceivedAt">The calendar date it reached the company.</param>
/// <param name="CreatedAt">When it entered the queue.</param>
/// <param name="ReceiptSentAt">When the receipt went out.</param>
/// <param name="DecisionDue">When the decision is due by.</param>
/// <param name="Status">Where it stands.</param>
/// <param name="DecidedAt">When it was decided, where it was.</param>
/// <param name="DecisionReason">Why it was refused, where it was.</param>
/// <param name="Channel">How an out-of-band request arrived.</param>
/// <param name="IdentityConfirmation">What confirmed the requester is the subject.</param>
/// <remarks>Implements chapter 09 section 8a and PRIV-RIGHT-002.</remarks>
internal sealed record PrivacyRequestView(
    Guid RequestId,
    Guid Subject,
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
    string? IdentityConfirmation);
