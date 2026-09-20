namespace Janus.Core;

/// <summary>
/// Where the words of every outbound message come from. The library writes none of
/// them: it names the message, the channel and the recipient's language, and the
/// deployment's catalogue answers with the text.
/// </summary>
/// <remarks>
/// Implements LIB-EXT-001, CONV-CONTENT-001, INT-SMS-005a and AUTH-ABUSE-005. Every
/// message the library sends exists in every configured language or startup fails,
/// so a recipient is never resolved to a language the catalogue cannot answer in.
/// </remarks>
public interface IMessageTemplates
{
    /// <summary>
    /// The text of one message in one language on one channel.
    /// </summary>
    /// <param name="message">What the message is for.</param>
    /// <param name="kind">The channel it goes out on.</param>
    /// <param name="language">The recipient's language, as a BCP 47 tag.</param>
    /// <returns>The template, or the failure where the catalogue holds none.</returns>
    Result<MessageTemplate> Find(MessageKind message, SendKind kind, string language);
}
