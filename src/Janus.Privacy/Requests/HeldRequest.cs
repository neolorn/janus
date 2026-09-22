using System;
using Janus.Core;

namespace Janus.Privacy.Requests;

/// <summary>
/// Every field of a request's row, as the store read it.
/// </summary>
/// <param name="Id">What it is held under.</param>
/// <param name="Subject">Whose it is.</param>
/// <param name="Type">What it asks for.</param>
/// <param name="Detail">What was written.</param>
/// <param name="ReceivedAt">The calendar date it reached the company.</param>
/// <param name="CreatedAt">When it entered the queue.</param>
/// <param name="DecisionDue">When the decision is due by.</param>
/// <param name="WarnAt">When the Normal alert is due.</param>
/// <param name="EscalateAt">When the High alert is due.</param>
/// <param name="Status">Where it stands.</param>
/// <param name="DecidedAt">When it was decided, where it was.</param>
/// <param name="DecisionReason">Why it was refused, where it was.</param>
/// <param name="Channel">How an out-of-band request arrived.</param>
/// <param name="IdentityConfirmation">What confirmed the requester was the subject.</param>
/// <param name="WarnedAt">When the Normal alert was raised, where it was.</param>
/// <param name="EscalatedAt">When the High alert was raised, where it was.</param>
/// <remarks>
/// Implements CONV-DESIGN-003. The store reads rows and never aggregates, so the one
/// place a row becomes a request is the aggregate factory.
/// </remarks>
internal sealed record HeldRequest(
    PrivacyRequestId Id,
    SubjectId Subject,
    PrivacyRequestType Type,
    string Detail,
    DateOnly ReceivedAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset DecisionDue,
    DateTimeOffset WarnAt,
    DateTimeOffset EscalateAt,
    PrivacyRequestStatus Status,
    DateTimeOffset? DecidedAt,
    string? DecisionReason,
    string? Channel,
    string? IdentityConfirmation,
    DateTimeOffset? WarnedAt,
    DateTimeOffset? EscalatedAt);
