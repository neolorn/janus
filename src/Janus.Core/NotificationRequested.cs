using System;

namespace Janus.Core;

/// <summary>
/// The library needs a message delivered.
/// </summary>
/// <param name="RaisedAt">When the send was accepted.</param>
/// <param name="IdempotencyKey">The key a consumer recognises a repeat by.</param>
/// <param name="Message">What the message is for.</param>
/// <param name="Kind">The channel it goes out on.</param>
/// <remarks>
/// Implements chapter 10 section 5b and LIB-EXT-001. Neither the destination nor the
/// text is on the event: they go to the registered transport and nowhere else.
/// </remarks>
public sealed record NotificationRequested(
    DateTimeOffset RaisedAt,
    string IdempotencyKey,
    MessageKind Message,
    SendKind Kind) : DomainEvent(RaisedAt, IdempotencyKey);
