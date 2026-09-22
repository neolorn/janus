using System;

namespace Janus.Core;

/// <summary>
/// What a subject is told the moment their request enters the queue.
/// </summary>
/// <param name="RequestId">What the request is held under.</param>
/// <param name="ReceiptSentAt">When the automatic receipt went out.</param>
/// <param name="DecisionDue">When the decision is due by.</param>
/// <remarks>
/// Implements PRIV-RIGHT-002 and chapter 09 section 7. A receipt is not a decision;
/// it starts nothing and stops nothing.
/// </remarks>
public sealed record PrivacyRequestReceipt(
    PrivacyRequestId RequestId,
    DateTimeOffset ReceiptSentAt,
    DateTimeOffset DecisionDue);
