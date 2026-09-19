using System;

namespace Janus.Core;

/// <summary>
/// A new-device check completed.
/// </summary>
/// <param name="RaisedAt">When the check passed.</param>
/// <param name="IdempotencyKey">The key a consumer recognises a repeat by.</param>
/// <param name="Browser">The browser the check was passed from.</param>
/// <remarks>
/// Implements AUTH-FACT-016 and chapter 10 section 5b. The event carries the browser
/// identifier and no personal data.
/// </remarks>
public sealed record DeviceVerified(
    DateTimeOffset RaisedAt,
    string IdempotencyKey,
    DeviceId Browser) : JanusEvent(RaisedAt, IdempotencyKey);
