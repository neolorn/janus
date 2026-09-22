using System;

namespace Janus.Hosting.Privacy;

/// <summary>
/// What the subject is told when their request enters the queue.
/// </summary>
/// <param name="RequestId">What the request is held under.</param>
/// <param name="ReceiptSentAt">When the automatic receipt went out.</param>
/// <param name="DecisionDue">When the decision is due by.</param>
/// <remarks>Implements chapter 09 section 7 and PRIV-RIGHT-002.</remarks>
internal sealed record PrivacyReceiptView(
    Guid RequestId,
    DateTimeOffset ReceiptSentAt,
    DateTimeOffset DecisionDue);
