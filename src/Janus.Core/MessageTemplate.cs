namespace Janus.Core;

/// <summary>
/// The text one message is sent as in one language, with a place for each value the
/// library puts in it written as the value's name in braces.
/// </summary>
/// <param name="Subject">
/// The line a mail carries above its body, and absent for a message sent by SMS.
/// </param>
/// <param name="Text">The message itself.</param>
/// <remarks>
/// Implements LIB-EXT-001, AUTH-ABUSE-005, INT-SMS-003, INT-SMS-005a. The words are
/// the deployment's; the library validates their length at startup and puts its
/// values in their places at send.
/// </remarks>
public sealed record MessageTemplate(string? Subject, string Text);
