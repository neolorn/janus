namespace Janus.Core;

/// <summary>
/// What a gateway's delivery report says, once the transport has read it.
/// </summary>
/// <param name="Reference">The reference the message was sent with.</param>
/// <param name="Delivered">
/// Whether the gateway says the message arrived; false for every final failure.
/// </param>
/// <remarks>
/// Implements INT-SMS-005 and INT-SMS-006. Neither value is trusted: the reference is
/// looked up by its hash, and a report of delivery changes nothing.
/// </remarks>
public sealed record SmsDeliveryReport(string Reference, bool Delivered);
