namespace Janus.Core;

/// <summary>
/// One mail as the transport receives it: where it goes and what it says, with every
/// value already in its place.
/// </summary>
/// <param name="Destination">The address it goes to.</param>
/// <param name="Subject">The line above the body.</param>
/// <param name="Body">The message itself.</param>
/// <param name="Reference">
/// What a delivery report about this mail quotes, and what the library recognises it
/// by. Unguessable, and never a value a person is shown.
/// </param>
/// <remarks>Implements INT-GEN-005 and LIB-EXT-001: one place builds the payload.</remarks>
public sealed record MailMessage(
    EmailAddress Destination,
    string Subject,
    string Body,
    string Reference);
