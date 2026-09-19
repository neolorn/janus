namespace Janus.Core;

/// <summary>
/// One text message as the transport receives it: where it goes and what it says,
/// with every value already in its place.
/// </summary>
/// <param name="Destination">The number it goes to.</param>
/// <param name="Text">The message itself.</param>
/// <param name="Reference">
/// What the gateway's delivery report quotes, and what the library recognises it by.
/// Unguessable, and never a value a person is shown.
/// </param>
/// <remarks>Implements INT-GEN-005, INT-SMS-005 and LIB-EXT-001.</remarks>
public sealed record SmsMessage(
    PhoneNumber Destination,
    string Text,
    string Reference);
